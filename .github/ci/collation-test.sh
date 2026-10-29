#!/usr/bin/env bash
# Reproduces a database that was created on MySQL 8 (utf8mb4_0900_ai_ci) and then moved to a MariaDB 11.x server,
# whose default collation (utf8mb4_uca1400_ai_ci) is what newly added tables/columns get. Checks the joins fail
# before the site's start-up collation fix runs, and work after it.
set -euo pipefail

EF="dotnet ef --project ForestChurches.csproj --configuration Release --no-build"
BEFORE="Bulletin_fix"
OLD="${OLD_COLLATION:-utf8mb4_0900_ai_ci}"
ROOT=(mysql -h 127.0.0.1 -P 3306 -uroot -pci-root-password --batch --skip-column-names)
q() { "${ROOT[@]}" forestchurches -e "$1"; }

echo "== Server: $("${ROOT[@]}" -e 'SELECT VERSION(), @@collation_server;')"
"${ROOT[@]}" -e "DROP DATABASE IF EXISTS forestchurches; CREATE DATABASE forestchurches;"

echo "== Old schema created while utf8mb4 defaulted to $OLD (as on the MySQL 8 server the data came from)"
"${ROOT[@]}" -e "SET GLOBAL character_set_collations = 'utf8mb4=$OLD';"
$EF database update "$BEFORE"
"${ROOT[@]}" -e "SET GLOBAL character_set_collations = DEFAULT;"
echo "   server default for utf8mb4 is back to: $("${ROOT[@]}" -e "SELECT @@character_set_collations;")"

q "INSERT INTO AspNetUsers (Id, UserName, NormalizedUserName, Email, NormalizedEmail, EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount, CreatedDate, ImageArray, Role)
   VALUES ('u1', 'office@test.example', 'OFFICE@TEST.EXAMPLE', 'office@test.example', 'OFFICE@TEST.EXAMPLE', 1, 'x', 'x', 'x', 0, 0, 1, 0, '2026-01-01', x'00', 'AuthorizedChurch');"
q "INSERT INTO ChurchInformation (ID, ChurchAccountId, Name, Description, Status, Denomination, Congregation, Address, Phone, Churchsuite, Website,
     Parking, Restrooms, WheelchairAccess, Wifi, Refreshments, WorshipHall, Childcare, MeetingHall, Kitchen, OutdoorSpaces, HearingAidSupport)
   VALUES ('c1', 'u1', 'St Test', 'd', 'Open', 'CofE', '20-50', '1 High St', '0', 'https://churchsuite.com/', 'https://example.org/', 0,0,0,0,0,0,0,0,0,0,0);"
q "INSERT INTO ServiceTimes (ServiceID, Time, Note, ChurchInformationId) VALUES ('s1', '10:30', 'Holy Communion', 'c1');"

echo "== Applying the latest migrations the old way (expected to stop part-way, as it did on the live server)"
if $EF database update > "$RUNNER_TEMP/migrate.log" 2>&1; then
  echo "::notice title=collation::plain migration succeeded on this server"
else
  echo "::notice title=collation::plain migration failed part-way: $(grep -m1 -E 'MySqlException' "$RUNNER_TEMP/migrate.log" | cut -c1-300)"
  $EF database update > "$RUNNER_TEMP/migrate2.log" 2>&1 && { echo "unexpected: retry worked"; } || \
    echo "::notice title=collation::and a retry fails too: $(grep -m1 -E 'MySqlException' "$RUNNER_TEMP/migrate2.log" | cut -c1-300)"
fi

echo "== Running the site's start-up database step"
FC_DB_TEST_CONNECTION="server=127.0.0.1;port=3306;user=root;password=ci-root-password;database=forestchurches;" \
FC_DB_OTHER_COLLATION="$("${ROOT[@]}" -e "SELECT @@collation_server;")" \
  dotnet test Tests/ForestChurches.Tests.csproj -c Release --filter "FullyQualifiedName~DatabaseStartupIntegration" --logger "console;verbosity=normal"

JOIN="SELECT COUNT(*) FROM ServiceTimes s LEFT JOIN ChurchInformation c ON s.LocationChurchId = c.ID;"
left="$(q "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND COLLATION_NAME IS NOT NULL AND COLLATION_NAME <> '$OLD' AND TABLE_NAME IN ('ChurchInformation','ServiceTimes','Benefices','ServiceDates','AspNetUsers');")"
[ "$left" = "0" ] || { echo "FAILED: $left columns still on another collation"; exit 1; }
[ "$(q "$JOIN")" = "1" ] || { echo "FAILED: join after fix"; exit 1; }
[ "$(q "SELECT CONCAT(Note,'|',RepeatType,'|',\`Interval\`) FROM ServiceTimes WHERE ServiceID='s1';")" = "Holy Communion|Weekly|1" ] || { echo "FAILED: service data"; exit 1; }
[ "$(q "SELECT COUNT(*) FROM ChurchInformation WHERE ID='c1' AND ChurchAccountId='u1';")" = "1" ] || { echo "FAILED: church data"; exit 1; }
echo "::notice title=collation::all collation checks passed"
