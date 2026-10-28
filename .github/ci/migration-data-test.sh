#!/usr/bin/env bash
# Checks the migrations against data, not just an empty database:
#   1. migrate to the schema before benefices/schedules and insert a church with an old-style service
#   2. apply the latest migrations and check the service now means "every Sunday, at its own church"
#   3. roll back and forward again
#   4. check an account can now have a second church
# Uses the database from the workflow's service container (see build-check.yml).
set -euo pipefail

EF="dotnet ef --project ForestChurches.csproj --configuration Release --no-build"
BEFORE="Bulletin_fix"   # last migration before benefices and service schedules
MYSQL=(mysql -h 127.0.0.1 -P 3306 -uroot -pci-root-password forestchurches --batch --skip-column-names)
q() { "${MYSQL[@]}" -e "$1"; }
expect() { # expect "<description>" "<sql>" "<expected>"
  local got; got="$(q "$2" | tr '\t' ' ' | tr -d '\r')"
  if [ "$got" != "$3" ]; then echo "FAILED: $1 -- expected [$3], got [$got]"; exit 1; fi
  echo "ok: $1"
}

echo "== Migrating to $BEFORE"
$EF database update "$BEFORE"

echo "== Inserting old-style data"
q "INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, CreatedDate, ImageArray, Role)
   VALUES ('u1', 'office@test.example', 'OFFICE@TEST.EXAMPLE', 'office@test.example', 'OFFICE@TEST.EXAMPLE', 1, 'x', 'x', 'x', 0, 0, 1, 0, '2026-01-01', x'00', 'AuthorizedChurch');"
q "INSERT INTO ChurchInformation (ID, ChurchAccountId, Name, Description, Status, Denomination, Congregation, Address, Phone, Churchsuite, Website,
     Parking, Restrooms, WheelchairAccess, Wifi, Refreshments, WorshipHall, Childcare, MeetingHall, Kitchen, OutdoorSpaces, HearingAidSupport)
   VALUES ('c1', 'u1', 'St Test', 'd', 'Open', 'CofE', '20-50', '1 High St', '0', 'https://churchsuite.com/', 'https://example.org/', 0,0,0,0,0,0,0,0,0,0,0);"
q "INSERT INTO ServiceTimes (ServiceID, Time, Note, ChurchInformationId) VALUES ('s1', '10:30', 'Holy Communion', 'c1');"

echo "== Applying the latest migrations"
$EF database update

expect "old service became weekly on Sunday at its own church" \
  "SELECT RepeatType, \`Interval\`, DayOfWeek, UseCustomLocation, IFNULL(LocationChurchId,'-'), IFNULL(WeeksOfMonth,'-') FROM ServiceTimes WHERE ServiceID='s1';" \
  "Weekly 1 0 0 - -"
expect "old church has no benefice" "SELECT IFNULL(BeneficeId,'-') FROM ChurchInformation WHERE ID='c1';" "-"
expect "Benefices table exists" "SELECT COUNT(*) FROM Benefices;" "0"
expect "ServiceDates table exists" "SELECT COUNT(*) FROM ServiceDates;" "0"

echo "== Rolling back to $BEFORE and forward again"
$EF database update "$BEFORE"
expect "service survived the rollback" "SELECT Note FROM ServiceTimes WHERE ServiceID='s1';" "Holy Communion"
$EF database update
expect "service survived re-applying" "SELECT RepeatType FROM ServiceTimes WHERE ServiceID='s1';" "Weekly"

echo "== New capabilities"
q "INSERT INTO Benefices (Id, Name, CreatedUtc) VALUES ('b1', 'The Test Benefice', NOW());"
q "INSERT INTO ChurchInformation (ID, ChurchAccountId, BeneficeId, Name, Description, Status, Denomination, Congregation, Address, Phone, Churchsuite, Website,
     Parking, Restrooms, WheelchairAccess, Wifi, Refreshments, WorshipHall, Childcare, MeetingHall, Kitchen, OutdoorSpaces, HearingAidSupport)
   VALUES ('c2', 'u1', 'b1', 'St Second', 'd', 'Closed', 'CofE', '20-50', '2 High St', '0', 'https://churchsuite.com/', 'https://example.org/', 0,0,0,0,0,0,0,0,0,0,0);"
expect "an account can have two churches" "SELECT COUNT(*) FROM ChurchInformation WHERE ChurchAccountId='u1';" "2"

q "INSERT INTO ServiceTimes (ServiceID, Time, Note, ChurchInformationId, RepeatType, \`Interval\`, DayOfWeek, StartDate, UseCustomLocation, LocationChurchId)
   VALUES ('s2', '18:00', 'Evensong', 'c1', 'Weekly', 2, 0, '2026-11-01', 1, 'c2');"
q "INSERT INTO ServiceTimes (ServiceID, Time, Note, ChurchInformationId, RepeatType) VALUES ('s3', '23:30', 'Midnight Mass', 'c2', 'Dates');"
q "INSERT INTO ServiceDates (Id, ServiceTimesId, Date, Time) VALUES ('d1', 's3', '2026-12-24', '23:30');"

q "DELETE FROM Benefices WHERE Id='b1';"
expect "deleting a benefice keeps its churches" "SELECT COUNT(*), IFNULL(MAX(BeneficeId),'-') FROM ChurchInformation WHERE ID='c2';" "1 -"

q "DELETE FROM ChurchInformation WHERE ID='c2';"
expect "deleting a church removes its services and dates" "SELECT (SELECT COUNT(*) FROM ServiceTimes WHERE ServiceID='s3') + (SELECT COUNT(*) FROM ServiceDates WHERE Id='d1');" "0"
expect "services held at a deleted church fall back (link cleared)" "SELECT IFNULL(LocationChurchId,'-') FROM ServiceTimes WHERE ServiceID='s2';" "-"

q "DELETE FROM AspNetUsers WHERE Id='u1';"
expect "deleting an account removes its churches and services" "SELECT (SELECT COUNT(*) FROM ChurchInformation WHERE ChurchAccountId='u1') + (SELECT COUNT(*) FROM ServiceTimes);" "0"

echo "All migration data checks passed."
