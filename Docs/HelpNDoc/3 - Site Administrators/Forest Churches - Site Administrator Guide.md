# Welcome, site administrators

This guide is for the people who run Forest Churches. Your account has the **SuperAdmin** role, which gives you **Site admin**: tools for managing users, roles, invitations, churches, events, bulletins and emails, and for checking the site is healthy.

You can also use everything church administrators can (see the **Church Administrator Guide**) and everything visitors can (see the **Visitor Guide**).

## Account levels at a glance

- **No role (public visitor)**: browses the site and contacts churches. No account needed. A new account that hasn't been approved yet also has no role.
- **AuthorizedChurch (church administrator)**: manages one church's page, services and events in **Church admin**.
- **Admin**: can open Site admin pages that need the *Admin.Read* permission.
- **SuperAdmin (site administrator)**: full access, including editing and deleting users and sending emails to users. SuperAdmin accounts appear in the user list as **Developer** and must never be deleted.

What each role can do is set by its permissions (see "Roles and permissions").

## Opening Site admin

1. Sign in with **Church login** in the header.
2. Select **Admin** in the header (or **Site admin →** at the bottom of the Church admin menu).

The top bar shows a **Site admin** tag. It also has links to **Church admin** and **Back to site**, the light/dark switch, your email address and **Logout**.

The menu on the left (a scrolling strip on phones) has: **Dashboard**, **Users**, **Roles**, **Invitations**, **Churches**, **Events**, **Bulletins**, **Emails**, **Email delivery**, **Logs** and **Changelog**.

After each action, a coloured message at the top of the page confirms what happened: green for success, red for a problem, amber for a warning.

# Dashboard

## At a glance

The **Dashboard** shows:

- **Churches**: how many are registered, and how many are listed in search (status Open).
- **Events**: the total, and how many are upcoming.
- **Pending invitations**: invitations not yet used to register.
- **Newest member**: the most recently created account and when it joined.
- **Recently added churches** and **Upcoming events**, each with a **View all** link.

**Invite a church** and **+ Add an event** at the top are shortcuts.

## System health

The **System health** cards show **CPU** and **Memory** use for the website (refreshed every 10 seconds while the page is open), plus the size of the main **Database** and the **Log database** in MB.

If you see "Live figures aren't available right now", the stats service couldn't be reached. The rest of the dashboard still works.

# Bringing a new church on board

## The full process

1. **Invite**: in **Invitations**, enter the church contact's email address and select **Send invitation**.
2. **They register**: the contact opens the emailed link (valid for 24 hours), creates an account with that same email address, and confirms their email.
3. **Approve**: in **Users**, find the new account (status **Waiting for approval**), select **Edit**, tick the **AuthorizedChurch** role and select **Save changes**. Giving a role also confirms the account.
4. **Check the listing**: in **Churches**, check the new church's details and open **View public page** to see what visitors see.

If the link expires before they register, select **Resend link** in **Invitations**. This sends a fresh 24-hour link.

# Users

## The user list

**Users** lists every account with:

- its profile picture, email address and phone number
- a status: **Developer** (SuperAdmin), **Administrator** (Admin), **Approved church** (AuthorizedChurch) or **Waiting for approval** (no role)
- all its **roles**
- whether the email address is confirmed

Each row has **Edit**, **Download** and **Delete**. Delete isn't offered for SuperAdmin accounts.

## Creating a user directly

Use this when someone needs an account without the invitation process, for example another administrator:

1. In **Users**, under **Create a user**, enter their **Email** and select **Continue**.
2. Choose a **Password** and confirm it. It must meet the password rules: 12+ characters, upper and lower case, a number and a symbol.
3. Tick their **Roles**.
4. Optionally add a **Profile picture**.
5. Select **Create a user**.

The account is confirmed immediately, a church listing is created for it, and the person gets a welcome email.

## Editing a user

Select **Edit** on a user (SuperAdmin only):

- **Email** and **Phone**: update their details.
- **Roles**: tick or untick roles. Giving any role also confirms the account.
- **Email confirmed**: when the user has no roles, this switch decides whether the account counts as confirmed.
- **New password**: set a new password for them. Leave it empty to keep their current one.

Select **Save changes**.

## Downloading or deleting a user

- **Download** saves the user's account data (email, phone, roles and picture) as a JSON file, which is useful for data requests.
- **Delete** permanently removes the account after you confirm. Only SuperAdmins can delete users. This can't be undone. To hide a church temporarily, use **Reset listing** in Churches, or ask the church to mark itself closed.

# Roles and permissions

## Managing roles

**Roles** lists every role, its ID and its permissions.

- **Create a role**: enter a **Role name** and select **Create role**.
- **Edit permissions**: switch individual permissions on or off for that role, then select **Save changes**.
- **Delete**: removes a role. A role that's still assigned to any user can't be deleted. Remove it from those users first.

Don't rename or delete the built-in roles (**SuperAdmin**, **Admin**, **AuthorizedChurch**). Parts of the site check for them by name.

# Invitations

## Inviting a church

Only invited email addresses can register a church account.

1. Open **Invitations**.
2. Enter the **Email** under **Invite a church** and select **Send invitation**.

They get an email with a registration link that lasts 24 hours.

## Tracking invitations

The list shows each invited address with its status:

- **Waiting**: invited, not registered yet. The **Link expires** column shows when the current link stops working.
- **Link expired**: the 24 hours have passed. Select **Resend link** to send a new one.
- **Registered**: they've created their account. The **Registered** column shows when.

**Remove** deletes an invitation (after you confirm), and its link stops working.

# Churches

## The church list

**Churches** shows every church listing: its status, denomination, congregation size, the account that owns it, address, phone, website, ChurchSuite link, facilities and (under **Description**) its description.

Statuses:

- **Listed**: open and visible in search.
- **Hidden**: marked closed by the church.
- **Not set up**: reset to defaults and waiting to be filled in.

## Actions

- **View public page ↗**: opens the church's public page (listed churches only).
- **Edit**: change any detail and its facilities, then select **Save changes**. Your changes update that church, and are visible immediately.
- **Download**: saves the church's details and service times as a JSON file.
- **Reset listing**: after you confirm, replaces the church's details with defaults and sets it to **Not set up**, which hides it from search. Use this for listings that are wrong or abandoned. The account itself isn't deleted.

# Events

## Managing events

**Events** shows every church's events in two sections: **Upcoming events** and **Past events**. Each shows the event name, location, date and time, repeat pattern, church and owner account.

- **+ Add an event**: create an event for any church. Choose the **Church account** it belongs to; the event is listed under that account's church. The other fields are the same as in the Church Administrator Guide.
- **Edit**: change any detail. Leave the image empty to keep the current picture.
- **Download**: saves the event as a JSON file.
- **Delete**: removes the event after you confirm.

The church gets an email when an event is created for it.

## Automatic clean-up

One-off events are removed about 24 hours after they finish. Owners of repeating events are emailed from time to time to confirm the event is still running. Events synced from ChurchSuite are refreshed from ChurchSuite.

# Bulletins

## Creating or editing a bulletin

1. Open **Bulletins** and select **+ New bulletin**, or **Edit** on an existing one.
2. Choose the **Type**:
   - **Announcement** or **Message**: a title, a message and an optional list of links or notices.
   - **Weekly bulletin (pew sheet)**: also shows the **Pew sheet** section, with **Parish or benefice**, **Sunday or service name**, **First reading**, **Gospel**, **Hymns** and **The Collect**.
3. Set the **Date** and **Title**.
4. Fill in **Message or pastoral note** and **Notices or links**. Put one notice or link per line; the same applies to hymns.
5. Optionally upload a **PDF version** for printing. Uploading a new one replaces the old one.
6. Select **Save changes**.

## Making a bulletin live

Only one bulletin is live at a time. The live one is what visitors see on the public **Bulletin** page.

In the bulletin list:

- **Make live** makes that bulletin the public one. The current live bulletin shows a **Live** badge.
- **Show banner** / **Hide banner** controls the site-wide banner that links to the bulletin. Showing the banner on one bulletin hides it on the others and makes that bulletin live.
- **Show banner until**: pick a date and time and select **Set**. The banner then hides itself after that time. Leave it empty to keep the banner up.
- **Delete** removes a bulletin after you confirm.

**View public page ↗** opens the public Bulletin page so you can check it.

# Emails

## Sending an email to users

**Emails** sends a message from info@forestchurches.co.uk (SuperAdmin only):

1. Choose who it's **Send to**: one user, or **All users**.
2. Enter your name in **Sign off as**. It appears as the sender's name at the end of the email.
3. Write the **Message**.
4. Select **Send message**.

A green message confirms the email was handed over. Use **Email delivery** to check it actually went out.

## Email delivery

All emails (invitations, confirmations, password resets, event emails, church details and your messages) are passed to the **email app** at api.forestchurches.co.uk. The email app queues them and sends them every 5 minutes.

**Email delivery** shows whether each one arrived:

- **Email app** card: whether the email app is **Working**, its response time, how many emails are **Waiting in queue**, when it last sent a batch, and its sent/failed totals. A **Problem** badge explains what's wrong, for example that it's unreachable or rejected the site's sign-in token.
- **Recent emails**: the newest 500 emails since the website last restarted. Each has a subject, recipient, tracking ID, status, number of attempts, the HTTP code returned, and the time.

What the statuses mean:

- **Handing over**: the website is still posting the email to the email app. It tries up to 3 times.
- **Received**: the email app confirmed it received the email.
- **Waiting to send**: the email app has it queued (or is retrying).
- **Sent**: the email app handed it to the mail server. If it hasn't arrived, ask the recipient to check their spam folder.
- **Not received**: all 3 attempts failed, so the email app never got it. The reason is shown underneath.
- **Couldn't send**: the email app received it, but the mail server refused it 3 times.
- **No record**: the email app doesn't know this email, usually because the email app restarted (its queue is kept in memory).

Actions:

- **Check** asks the email app for the latest status of one email.
- **Check unfinished** checks every email that has been received but not yet sent or failed.
- **Logs** opens the log entries for that email's tracking ID.
- **Refresh** re-runs the email app health check.

## Investigating a missing email

1. Open **Email delivery** and find the email by recipient and subject.
2. If it's **Not received**, the email app was unreachable. Look at the **Email app** card, and restart or redeploy the email app if needed. Then ask the person to repeat what they were doing (for example request the password reset again).
3. If it's **Received** or **Waiting to send**, select **Check**. Emails go out within about 5 minutes.
4. If it's **Couldn't send**, read the error: a mistyped address or a template problem is the usual cause.
5. If it's **Sent**, the email left our system. Ask the recipient to check spam and add info@forestchurches.co.uk to their contacts.
6. If the email is older than the list, open **Logs** and search for the recipient's address or the email's tracking ID.

# Logs and maintenance

## Logs

**Logs** shows the application's log entries, newest first. New entries appear at the top while the page is open.

- **Search the logs**: type text (an email address, an email tracking ID, an error message) and select **Search**.
- Select a row to see its full details.
- **Previous** / **Next** move between pages.
- **Download logs**: pick **From** and **To** dates and times, then select **Download logs** for a file of those entries (filtered by any search text).

Colours: grey for debug, amber for warnings, red for errors.

## Changelog

**Changelog** lists what changed in each release of the site, newest first.

## Background tasks

- **Event clean-up** runs on a schedule inside the website. It removes finished one-off events and emails the owners of repeating events. To see when it last ran, search **Logs** for `SortAndDeleteEvents`.
- **Queued jobs** run in Hangfire. Its dashboard is at **/hangfire** and has its own username and password. Use it to see recent and failed jobs, and to retry a failed job.

# Security and good practice

## Keeping the site safe

- Turn on **two-factor authentication** for every administrator account (Church admin → Two-Factor Authentication).
- Give people the lowest role they need. Most church contacts only need **AuthorizedChurch**.
- Don't delete or rename SuperAdmin accounts or the built-in roles.
- Remove invitations that are no longer needed.
- Check **Email delivery** and **Logs** after changes to email or hosting settings.
- Keep secrets (database passwords, API keys, client secrets, JWT signing keys) in Azure Key Vault, never in source code or appsettings files. If a secret has ever been committed to a repository, rotate it.

## Troubleshooting

- **A church can't open Church admin**: check in **Users** that the account has the **AuthorizedChurch** role and is confirmed.
- **A church says they never got their invitation**: check **Email delivery**, then **Resend link** in **Invitations**.
- **A church doesn't appear in search**: in **Churches**, check it's **Listed** and that its address and postcode are correct.
- **"Access denied" on a Site admin page**: that page needs a permission your role doesn't have. Check the role's permissions in **Roles**.
- **The Email app card shows a problem**: if it's unreachable, check the email app is running at api.forestchurches.co.uk. If the token is rejected, check both apps use the same JWT issuer, audience and signing key. If it's an older version without delivery checks, deploy the latest EmailManagement-ForestChurches.
