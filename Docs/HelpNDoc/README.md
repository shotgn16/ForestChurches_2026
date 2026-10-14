# Forest Churches help documentation (for HelpNDoc)

There are three separate guides, one for each level of access:

| Folder | Guide | Who it's for | Role |
|---|---|---|---|
| `1 - Visitors` | Visitor Guide | Anyone using the public site | No role (not signed in) |
| `2 - Church Administrators` | Church Administrator Guide | People who look after a church's page | AuthorizedChurch |
| `3 - Site Administrators` | Site Administrator Guide | People who run the website | SuperAdmin |

Each guide is a single Markdown file:

- `#` headings are chapters.
- `##` headings are topics inside a chapter.

## Importing into HelpNDoc

To keep the three guides separate, create one HelpNDoc project per guide:

1. In HelpNDoc, select **File → New project**. Name it, for example, "Forest Churches – Visitor Guide".
2. Select **File → Import**, choose **Import files**, and pick the guide's `.md` file.
3. When asked how to import, choose to **split the document by heading levels**, using heading levels 1 and 2. HelpNDoc creates a chapter for each `#` heading and a topic for each `##` heading, in the same order as the file.
4. Check the table of contents, then build the documentation (HTML, CHM, PDF, Word…) as usual.

Repeat for the other two guides.

If you'd rather have one project with three sections, create three top-level topics (Visitors, Church Administrators, Site Administrators) and import each file underneath its section.

## Keeping the guides up to date

The guides describe the site as of the October 2026 redesign (release 2.0.0). Button and page names in **bold** match the English wording on the site. When a page changes, update the matching `##` topic and re-import, or edit the topic directly in HelpNDoc.

Screenshots aren't included, so that the text stays accurate when the design changes. If you want them, add them in HelpNDoc's Library and insert them into the relevant topics.
