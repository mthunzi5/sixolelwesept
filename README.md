# RaceDay

RaceDay is a race-day event management system. Organisers create running/athletic
events (e.g. a city marathon), break each event into entry categories (5km, 10km,
21km, etc.), and Participants browse events, enrol in a category, pay their entry
fee, and receive a result once the race has been run.

This repository currently covers **Part 1 - System Planning and Database**: the
system was fully planned - ERD, API endpoint plan, and database schema - before any
application code was written, as required by the brief.

## Roles

| Role | Can do |
|---|---|
| **Organiser** | Register/log in, create and manage their own events, add categories to their events, view the enrolment roster for a category, capture and correct race results, view payment status for their events. |
| **Participant** | Register/log in, browse public events and categories, enrol in a category, view and manage their own enrolments and payments, view their own results and public event leaderboards. |

Both roles share the `Users` table (see `docs/erd.png`) and are distinguished by a
`Role` column; every endpoint in `docs/endpoint-plan.md` states which role(s) may
call it, and ownership checks (an Organiser may only manage *their own* events) are
enforced on top of the role check.

## Repository structure

```
/docs
  erd.png              - Entity Relationship Diagram (Section A)
  endpoint-plan.md      - Full API endpoint plan (Section B)
  raceday_schema.sql    - SQL Server schema + seed data script (Section C)
/.github/workflows
  validate-docs.yml     - CI check that the required planning docs exist
```

## Database

`docs/raceday_schema.sql` creates a `RaceDayDB` database in SQL Server with six
tables matching the ERD exactly - `Users`, `Events`, `Categories`, `Enrolments`,
`Results`, `Payments` - with primary keys, foreign keys, `UNIQUE`/`CHECK`/`DEFAULT`
constraints, and seed data (2 organisers, 4 participants, 3 events, 7 categories
across those events, and sample enrolments/payments/results).

To run it: open the file in SSMS connected to any SQL Server instance and execute
the whole script. It was verified end-to-end against a clean LocalDB instance via
`sqlcmd` before being committed - no errors, and the row-count summary at the end
of the script confirms the seed data landed correctly.

## CI/CD

A GitHub Actions workflow (`.github/workflows/validate-docs.yml`) runs on every
push/PR to `main` and fails the build if `docs/` is missing any of the three
required planning files, or if `README.md` is missing from the repo root.

**Screenshot of a successful green build:**

> _TODO: push this repo to GitHub, let the workflow run once, then paste a
> screenshot of the green check here, e.g._
> `![CI passing](docs/ci-passing.png)`

## Video walkthrough

> _TODO: record an unlisted YouTube video walking through the ERD decisions, the
> endpoint plan choices, and running `raceday_schema.sql` live in SSMS, then paste
> the link here._
>
> YouTube link: `<add link here>`

## Notes on plan vs. implementation

Section B requires that unexplained differences between this plan and the Part 2
implementation be documented. Nothing has been implemented yet, so there are no
deviations to record - this section will be updated once Part 2 begins.
