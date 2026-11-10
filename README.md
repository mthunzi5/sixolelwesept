# RaceDay

RaceDay is a race-day event management system. Organisers create running, walking and
cycling events (e.g. a city marathon), break each event into age or distance categories
(Under 20, Senior, 5km, 10km, 21km, ...), and Participants browse events, enrol in a
category, pay their entry fee, and view their results once the race has been run.

| Part | What it covers | Where |
|---|---|---|
| **Part 1** - System Planning and Database | ERD, API endpoint plan, SQL Server schema + seed script | [`docs/`](docs) |
| **Part 2** - RESTful API Development | ASP.NET Core Web API, EF Core Code-First, session auth, Swagger, unit tests, CI/CD | [`src/RaceDay.Api`](src/RaceDay.Api), [`tests/`](tests) - branch **`part2-api`** |
| **Part 3 (POE)** - Automation of Web Application | ASP.NET Core MVC front end, role-based UI, Azure Blob Storage images, Docker | [`src/RaceDay.Mvc`](src/RaceDay.Mvc), [`docker-compose.yml`](docker-compose.yml) - branch **`part3-mvc`** |

---

## Part 3 - RaceDay web application (POE)

![Build and test RaceDay](https://github.com/mthunzi5/sixolelwesept/actions/workflows/dotnet-ci.yml/badge.svg?branch=part3-mvc)

An ASP.NET Core MVC website for Organisers and Participants. It **consumes the Part 2 API for every
read and write**: the MVC project has no database, no Entity Framework and no business rules of its
own. Event banners and profile pictures are stored in **Azure Blob Storage** through the API, and the
whole system runs in **Docker**.

### Run it with Docker (one command)

Install [Docker Desktop](https://www.docker.com/products/docker-desktop/), then from the repository root:

```bash
docker compose up --build
```

| Open | What it is |
|---|---|
| **http://localhost:8080** | RaceDay website (MVC) |
| http://localhost:5272/swagger | RaceDay API (Swagger UI) |

This starts four containers: the **MVC web app**, the **API**, **SQL Server 2022**, and **Azurite**
(Microsoft's local Azure Blob Storage emulator). The API creates the database and seeds sample data on
first start, which takes about 30 seconds while SQL Server warms up. Stop everything with `Ctrl+C`, or
`docker compose down` (add `-v` to also wipe the database and stored images).

**Use a real Azure Storage account** instead of the emulator: copy its connection string from the Azure
portal (*Storage account → Security + networking → Access keys*), then:

```powershell
$env:AZURE_STORAGE_CONNECTION_STRING = "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
docker compose up --build
```

Uploaded images then appear in the `event-banners` and `profile-pictures` containers of that account.

**Run only the MVC container** against an API that is already running on your machine:

```bash
dotnet run --project src/RaceDay.Api --launch-profile http --urls http://0.0.0.0:5272   # terminal 1: the API
docker build -t raceday-web src/RaceDay.Mvc && docker run --rm -p 8080:8080 --add-host=host.docker.internal:host-gateway -e RaceDayApi__BaseUrl=http://host.docker.internal:5272 raceday-web   # terminal 2
```

### Run it without Docker

```bash
# terminal 1 - API (SQL Server LocalDB; Azure Blob via Azurite or ConnectionStrings:AzureBlobStorage)
dotnet run --project src/RaceDay.Api --launch-profile http

# terminal 2 - website
dotnet run --project src/RaceDay.Mvc --launch-profile http
```

Then open http://localhost:5080. For image uploads in this mode, either start Azurite (`npx azurite-blob`,
or the *Azurite* VS Code extension → "Start Blob Service"), or put a real Azure Storage connection string in
`src/RaceDay.Api/appsettings.json` → `ConnectionStrings:AzureBlobStorage`. If storage isn't available, the
site still works, and uploads show a friendly "image storage unavailable" message.

**Demo accounts** (password `Password123!`): Organiser `naledi.khumalo@raceday.co.za`, Participant `amahle.nkosi@example.com`.

### What each role sees

Each role gets its own navigation bar, landing page and pages. Pages are protected by the
[`[RequireRole]`](src/RaceDay.Mvc/Auth/RequireRoleAttribute.cs) filter: signed-out users go to the login
page (and back again afterwards), and the wrong role goes to an *Access denied* page. The API enforces the
same rules again on every call.

| Organiser | Participant |
|---|---|
| **Dashboard**: events created, total enrolments, enrolments per event, upcoming dates | **Home**: upcoming events with search, type and date filters |
| **My Events**: create / edit / delete with name, description, date, location, distance and type, plus **banner image upload** | **Event page**: full information, banner and available categories |
| **Categories**: add or remove **age or distance** categories per event | **Enrol**: choose a category, and the enrolment is recorded immediately with a bib number |
| **Enrolments**: every participant, their category and colour-coded status, filterable by category | **My Enrolments**: every event entered with status, plus pay or withdraw |
| **Results**: capture finish times and positions after the event, then **publish** (marks the event Completed) | **My Results**: event, date, category, finish time and placing, e.g. *47th of 312 finishers* |
| **Profile** | **Profile**: update details, **upload a profile picture**, change password |

Status colours are the same everywhere: **green** Confirmed / Paid / Finished, **amber** Pending / DNF,
**grey** Withdrawn / Refunded, **red** DSQ / Full.

### How the MVC app talks to the API (session management)

1. **Login**: `AuthController` posts the credentials to `POST /api/auth/login`. The API checks the BCrypt
   hash and returns its own session cookie (`.RaceDay.Session`).
2. `RaceDayApiClient` saves that cookie in the user's **MVC session**, together with their user ID, name and role.
   The browser only holds the website's own HttpOnly cookie (`.RaceDay.Web.Session`).
3. Every later API call for that user forwards their API cookie, so the API knows who is calling and
   applies its role and ownership rules. The shared `HttpClient` has cookies **disabled**, so one user's
   session can never leak to another.
4. If the API ever answers 401 (session expired), the website clears the session and sends the user to log in again.
5. **Logout** calls `POST /api/auth/logout`, clears the MVC session and redirects to the login page.

### Azure Blob Storage

All file handling goes **through the API**. The MVC app never contacts Azure.

```
Browser --upload--> MVC (OrganiserController / ProfileController)
                     --multipart POST--> API  POST /api/events/{id}/banner
                                              POST /api/users/me/profile-picture
                                               --> AzureBlobImageStorage --> Azure Blob container
Browser <--image--  MVC /media/{container}/{file} <-- API GET /api/images/{container}/{file} <-- Azure Blob
```

- [`src/RaceDay.Api/Services/AzureBlobImageStorage.cs`](src/RaceDay.Api/Services/AzureBlobImageStorage.cs) uses the
  `Azure.Storage.Blobs` SDK. It creates the private containers `event-banners` and `profile-pictures` on first use,
  and uploads with a random GUID name and the correct content type.
- [`ImagesController`](src/RaceDay.Api/Controllers/ImagesController.cs) checks ownership (only an event's organiser can
  change its banner), file type (JPG / PNG / WebP) and size (5 MB). It saves the image's URL on the `Events.BannerImageUrl`
  or `Users.ProfileImageUrl` column (migration `AddImageUrls`), and streams images back out via `GET /api/images/...`.
- The event detail page and event cards show the banner. The navbar and profile page show the profile picture.
- If storage is down or not configured, the API returns 503 and the website shows a friendly message instead of crashing.

### MVC project structure

```
src/RaceDay.Mvc/
  Program.cs                   MVC + session + typed HttpClient (cookies off) + friendly error pages + antiforgery
  Dockerfile, .dockerignore    multi-stage, non-root container image
  Services/
    RaceDayApiClient.cs        every API call (auth, events, categories, enrolments, results, payments, images)
    ApiResult.cs               success / friendly error / status code returned to controllers
  Auth/
    SessionKeys.cs             session values + helpers (IsSignedIn, GetUserRole, SignIn)
    RequireRoleAttribute.cs    page protection: login redirect / access denied
  Controllers/
    RaceDayControllerBase.cs   flash messages, expired-session handling
    HomeController.cs          upcoming events + filters, event details, leaderboard, error pages
    AuthController.cs          register, login, logout, access denied
    ParticipantController.cs   enrol, my enrolments (pay / withdraw), my results
    OrganiserController.cs     dashboard, events CRUD + banner, categories, enrolments, results entry + publish
    ProfileController.cs       profile details, picture upload, password change (both roles)
    MediaController.cs         /media/... image proxy to the API
  Models/                      API response shapes, validated form models, page view models
  Helpers/Display.cs           shared formatting: dates, money, finish times, "47th", badge colours
  Views/                       Razor views; Shared/_Layout.cshtml holds the role-specific navigation
  wwwroot/css/site.css         RaceDay brand theme (colours, typography, badges) on top of Bootstrap 5
```

### UI design

- **Brand**: night-track navy, race-bib orange and finish-line lime, with condensed athletic headings
  (Barlow Condensed) and Inter for body text. A chequered finish-line hero sits on the home page.
- **Bootstrap 5.3** for layout and responsiveness, plus Bootstrap Icons. All colours, radii and spacing come from
  CSS variables in `site.css`, so every page looks the same.
- **Invalid input** is caught by form validation (client and server side), and API errors are shown as readable
  messages. Unknown pages and unexpected errors show branded error pages, never stack traces.

### CI/CD

[`.github/workflows/dotnet-ci.yml`](.github/workflows/dotnet-ci.yml) now also **builds and publishes the MVC
project**, runs all the tests, then builds both Docker images and validates `docker-compose.yml`.

**Screenshot of a successful green build:**

![CI passing](docs/ci-part3.png)

### Video walkthrough (Part 3)

Unlisted YouTube demo of the full running application, the MVC structure, Blob Storage integration and Docker setup:

**YouTube link:** `<add link here>`

### Changes to the API for Part 3

| Change | Why |
|---|---|
| `Events.BannerImageUrl` and `Users.ProfileImageUrl` columns (migration `AddImageUrls`) | Store where each uploaded image lives |
| `POST /api/events/{id}/banner`, `POST /api/users/me/profile-picture`, `GET /api/images/{container}/{file}` | Azure Blob Storage uploads and downloads, so the MVC app never talks to Azure |
| `enrolmentCount` on events | Organiser dashboard: total enrolments per event |
| `entryFee`, `eventDate`, `paymentStatus`, `resultId`, `finishTimeSeconds`, `position`, `resultStatus` on enrolments | My Enrolments and the results entry page, without extra calls |
| `eventDate`, `finishersInCategory` on results | My Results: "47th of 312 finishers" |
| SQL retry on startup, plus a Dockerfile | Lets the API wait for the SQL Server container in docker-compose |

---

## Part 2 - RaceDay API

![Build and test API](https://github.com/mthunzi5/sixolelwesept/actions/workflows/dotnet-ci.yml/badge.svg?branch=part2-api)

### Tech stack

- **ASP.NET Core Web API** (.NET 9, C#) with controllers
- **Entity Framework Core 9**, Code-First, against **SQL Server** (LocalDB by default)
- **Session-based authentication** (`Microsoft.AspNetCore.Session`), passwords hashed with **BCrypt**
- **Swagger / OpenAPI** (Swashbuckle) with XML-comment descriptions on every endpoint
- **xUnit** unit + integration tests (EF Core InMemory, `WebApplicationFactory`)
- **GitHub Actions** CI that builds and runs all tests on every push

### Setup and run

**Prerequisites**

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server. **LocalDB** is enough: it ships with Visual Studio, or use the
  [SQL Server Express](https://www.microsoft.com/sql-server/sql-server-downloads) installer and pick "LocalDB".
  Check it's there with `sqllocaldb info` (should list `MSSQLLocalDB`).
- Any editor: VS Code (with the C# Dev Kit extension), Visual Studio 2022 or Rider.

**Run it**

```bash
git clone https://github.com/mthunzi5/sixolelwesept.git
cd sixolelwesept
git checkout part2-api

dotnet restore
dotnet run --project src/RaceDay.Api --launch-profile https
```

Then open **https://localhost:7048/swagger** (or http://localhost:5272/swagger with the `http` profile).

On first start-up the API **applies the EF Core migration automatically** (creating the `RaceDayDB`
database) and **seeds sample data**, so there is no manual database step. To create or update the
database by hand instead:

```bash
dotnet tool restore                                   # installs dotnet-ef from .config/dotnet-tools.json
dotnet ef database update --project src/RaceDay.Api
```

To start again from a clean database: `dotnet ef database drop --project src/RaceDay.Api -f`, then run the API.

> To use a different SQL Server, change `ConnectionStrings:RaceDayDb` in
> [`src/RaceDay.Api/appsettings.json`](src/RaceDay.Api/appsettings.json).
> The API manages its own schema through migrations, so don't run the Part 1 `raceday_schema.sql`
> against the same database name.

**Seeded accounts** (all use the password `Password123!`)

| Role | Email |
|---|---|
| Organiser | `naledi.khumalo@raceday.co.za` (owns the Johannesburg and Durban events) |
| Organiser | `sipho.dlamini@raceday.co.za` (owns the Cape Town event) |
| Participant | `amahle.nkosi@example.com`, `jason.vanwyk@example.com`, `palesa.mokoena@example.com`, `ryan.govender@example.com` |

**Run the tests**

```bash
dotnet test
```

### The two roles

Both roles live in the same `Users` table and are told apart by the `Role` column. The role is
**chosen at registration** (`"role": "Organiser"` or `"role": "Participant"`) and can't be changed later,
so a Participant can't promote themselves to Organiser.

| Role | Can do |
|---|---|
| **Organiser** | Register / log in, view and update own profile, **create, update and delete their own events** (name, description, date, location, distance, type: Run / Walk / Cycle), **define age or distance categories** for those events, **view all enrolments** for their events (per event or per category), **capture and correct finish times and positions** after an event, view payment status for their events. |
| **Participant** | Register / log in, view and update own profile, browse events and categories, **enrol in an event by choosing a category** (gets a bib number), view and withdraw their own enrolments, pay the entry fee, **view their own results**. |
| *Public (not logged in)* | Register, log in, browse events, categories and published results. |

Organiser endpoints return **403 Forbidden** to a Participant and vice versa. Every protected endpoint
returns **401 Unauthorized** when nobody is logged in. On top of the role check there's an
**ownership check**: an Organiser can only manage *their own* events, categories and results, and
a Participant can only see or change *their own* enrolments and payments.

### How authentication works (sessions)

1. `POST /api/auth/register` stores the user with `BCrypt.HashPassword(password)`. The plain-text
   password is never stored or returned.
2. `POST /api/auth/login` checks the password with `BCrypt.Verify`. On success it writes `UserId`, `Role`
   and `FullName` into the **server-side session** (`HttpContext.Session`) and the response sets an
   **HttpOnly `.RaceDay.Session` cookie** that holds only the session ID.
3. The browser (and Swagger UI) sends that cookie on every later request. The custom
   [`[SessionAuthorize]`](src/RaceDay.Api/Auth/SessionAuthorizeAttribute.cs) filter reads the
   session before the action runs:
   - no session: **401**
   - `[SessionAuthorize(UserRole.Organiser)]` called by a Participant (or the reverse): **403**
   - `[SessionAuthorize]` with no role: any logged-in user
4. `POST /api/auth/logout` clears the session. Sessions also expire after 30 minutes idle.

**Testing in Swagger:** call `POST /api/auth/login` with one of the seeded accounts, then use any
other endpoint and it runs as that user. Call `POST /api/auth/logout` to switch accounts. Each
endpoint's description in Swagger starts with **Access:** (Public / Any logged-in user / Organiser only /
Participant only).

### Code structure

```
RaceDay.sln
src/RaceDay.Api/
  Program.cs                  DI setup: EF Core, session, JSON, Swagger; auto-migrate + seed on start-up
  appsettings.json            SQL Server connection string
  Models/                     EF Core entities: User, Event, Category, Enrolment, Result, Payment + enums
  Data/
    RaceDayDbContext.cs       Fluent API mapping, matching the Part 1 schema (names, types, keys, CHECKs)
    DbSeeder.cs               Part 1 sample data, with real BCrypt hashes
    Migrations/               Code-First migration (InitialCreate)
  Auth/
    SessionKeys.cs            Names of the session values
    SessionAuthorizeAttribute.cs   Role-based authorisation filter (401 / 403)
  Controllers/
    ApiControllerBase.cs      CurrentUserId / CurrentRole from session + ProblemDetails helpers
    AuthController.cs         register, login, session, logout
    UsersController.cs        own profile (view / update / change password), public profiles
    EventsController.cs       events CRUD, organiser's events, all enrolments for an event
    CategoriesController.cs   age / distance categories per event
    EnrolmentsController.cs   enrol, my enrolments, category roster, withdraw
    ResultsController.cs      capture / correct results, leaderboard, my results
    PaymentsController.cs     pay entry fee, payment status
  Dtos/                       Request / response models (validation attributes) + entity mappings
  Swagger/SessionAuthOperationFilter.cs   Adds "Access:" + 401/403 responses to each Swagger operation
tests/RaceDay.Tests/
  Helpers/                    In-memory DbContext, fake ISession, WebApplicationFactory host
  AuthControllerTests.cs      registration + login (unit and HTTP)
  EventsControllerTests.cs    organiser event management
  RoleAccessTests.cs          [SessionAuthorize] unit tests + every protected endpoint with the wrong role / no login
  EnrolmentsControllerTests.cs   enrolment is recorded correctly, duplicate / full / closed rules, rosters
  ResultsCategoriesAndProfileTests.cs   results, categories, profile updates
.github/workflows/
  dotnet-ci.yml               Build + test on every push
  validate-docs.yml           Part 1 docs check
docs/                         Part 1 deliverables + EF-generated SQL (raceday_schema_efcore.sql)
```

### Endpoints

All of these are visible and testable in Swagger UI at `/swagger`, each with a description, its
expected request body or parameters, and every response code it can return.

| Method | Route | Access | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Create an Organiser or Participant account |
| POST | `/api/auth/login` | Public | Log in and start a session |
| GET | `/api/auth/session` | Any | Who is logged in on this session |
| POST | `/api/auth/logout` | Public | End the session |
| GET | `/api/users/me` | Any | View own profile |
| PUT | `/api/users/me` | Any | Update own name / phone number |
| PUT | `/api/users/me/password` | Any | Change own password |
| GET | `/api/users/{id}` | Any | Another user's public profile |
| GET | `/api/events` | Public | List events (filters: `status`, `eventType`, `from`, `to`) |
| GET | `/api/events/{id}` | Public | Event with its categories |
| GET | `/api/organisers/me/events` | Organiser | Events I organise |
| POST | `/api/events` | Organiser | Create event |
| PUT | `/api/events/{id}` | Organiser (owner) | Update event |
| DELETE | `/api/events/{id}` | Organiser (owner) | Delete event (409 if it has enrolments) |
| GET | `/api/events/{id}/enrolments` | Organiser (owner) | All enrolments for my event |
| GET | `/api/events/{eventId}/categories` | Public | Categories of an event |
| GET | `/api/categories/{id}` | Public | One category |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) | Add age / distance category |
| PUT | `/api/categories/{id}` | Organiser (owner) | Update category |
| DELETE | `/api/categories/{id}` | Organiser (owner) | Delete category (409 if it has enrolments) |
| POST | `/api/categories/{categoryId}/enrolments` | Participant | Enrol in an event by choosing a category |
| GET | `/api/participants/me/enrolments` | Participant | My enrolments |
| GET | `/api/categories/{categoryId}/enrolments` | Organiser (owner) | Category roster |
| GET | `/api/enrolments/{id}` | Participant (owner) / Organiser (owner) | One enrolment |
| DELETE | `/api/enrolments/{id}` | Participant (owner) / Organiser (owner) | Withdraw enrolment |
| GET | `/api/events/{eventId}/results` | Public | Event leaderboard |
| GET | `/api/participants/me/results` | Participant | My results |
| POST | `/api/enrolments/{enrolmentId}/result` | Organiser (owner) | Capture finish time and position |
| PUT | `/api/results/{id}` | Organiser (owner) | Correct a result |
| POST | `/api/enrolments/{enrolmentId}/payment` | Participant (owner) | Pay entry fee |
| GET | `/api/enrolments/{enrolmentId}/payment` | Participant (owner) / Organiser (owner) | Payment status |

Errors use the standard `ProblemDetails` JSON shape (`status`, `title`, `detail`).

### Unit tests

**101 tests**, all passing locally and in GitHub Actions. They use two styles:

- **Controller unit tests** create a controller directly with an isolated EF Core InMemory database
  and a fake session (`TestSession`), then assert on both the HTTP result **and what was saved**.
- **HTTP integration tests** host the whole API in memory with `WebApplicationFactory`, covering the real
  middleware, session cookie, `[SessionAuthorize]` filter and model validation, with SQL Server swapped for InMemory.

| Required area | Covered by |
|---|---|
| User registration and login | `AuthControllerTests`: success for both roles, BCrypt hash stored (not plain text), duplicate email 409, invalid role 400, login sets session, wrong password / unknown email 401, register then login then cookie works, logout |
| Organisers can manage events | `EventsControllerTests`: create 201 (owned by organiser, name/date/location/distance/type saved), past date 400, update by owner 200, update/delete by another organiser 403, missing 404, delete 204 (with categories), delete with enrolments 409, "my events", filters |
| Different roles are rejected | `RoleAccessTests`: filter unit tests (401 / 403 / allowed), **every** Organiser-only endpoint called by a Participant is 403, **every** Participant-only endpoint called by an Organiser is 403, **every** protected endpoint without login is 401, and rejected calls save nothing |
| Participants can enrol and it's recorded correctly | `EnrolmentsControllerTests`: 201 with participant, category, event and bib stored, unique bibs, duplicate / second category / full / closed event 409, missing category 404, withdraw and re-enrol, organiser roster, HTTP end-to-end flow |

### CI/CD

[`.github/workflows/dotnet-ci.yml`](.github/workflows/dotnet-ci.yml) runs on **every push to any branch**
and on pull requests: checkout, set up .NET 9, `dotnet restore`, `dotnet build -c Release`, then
`dotnet test`. It uploads the `.trx` test results as a build artifact.

**Screenshot of a successful green build:**

![CI passing](docs/ci-part2.png)

### Video walkthrough

Unlisted YouTube demo of the running API, code structure, endpoints, authentication and unit tests:

**YouTube link:** `<add link here>`

### Differences from the Part 1 plan

| Part 1 plan | Part 2 implementation | Why |
|---|---|---|
| Login issues a **JWT** sent as `Authorization: Bearer` | Login creates a **server-side session**, identified by an HttpOnly cookie | The Part 2 brief requires session management to keep the user's authenticated state and role. The `UserId` / `Role` the token would have carried now live in the session, so the role and ownership rules are unchanged. |
| `Events` had no distance or type | Added `DistanceKm` (decimal(5,2), > 0) and `EventType` (`Run` / `Walk` / `Cycle`, CHECK constraint) | Part 2 requires every event to capture distance and event type. |
| `Categories` were distance-only | Added `CategoryType` (`Distance` / `Age`) and optional `MinAge` / `MaxAge` | Part 2 requires age **or** distance categories (e.g. Under 20, Senior, 10km, 21km). |
| `PUT /api/users/me/password` | Implemented as planned | No change |
| (not planned) | `POST /api/auth/logout`, `GET /api/auth/session` | Needed to end or inspect a session. With JWT, logout was handled client-side. |
| (not planned) | `GET /api/events/{id}/enrolments` | Part 2 requires Organisers to view **all** enrolments for their events. The plan only had the per-category roster, which is still there. |
| `DELETE /api/enrolments/{id}` "withdraws/cancels" | Sets the enrolment's status to `Withdrawn` (row kept, paid fee marked `Refunded`). Re-enrolling reactivates it. | Keeps bib numbers unique and the history intact, and respects the `UNIQUE (ParticipantID, CategoryID)` key. |
| Seed data had placeholder password hashes | Seeder writes real BCrypt hashes (password `Password123!`) | So the seeded accounts can actually log in. |

Everything else (tables, columns, keys, unique and CHECK constraints, routes, role rules and status
codes) follows `docs/erd.png`, `docs/endpoint-plan.md` and `docs/raceday_schema.sql`. The SQL that the
EF Core migration produces is exported to [`docs/raceday_schema_efcore.sql`](docs/raceday_schema_efcore.sql)
for side-by-side comparison.

---

## Part 1 - System Planning and Database

The system was fully planned (ERD, API endpoint plan and database schema) before any application
code was written.

```
/docs
  erd.png                   Entity Relationship Diagram (Section A)
  endpoint-plan.md          Full API endpoint plan (Section B)
  raceday_schema.sql        SQL Server schema + seed data script (Section C)
```

`docs/raceday_schema.sql` creates a `RaceDayDB` database with six tables matching the ERD:
`Users`, `Events`, `Categories`, `Enrolments`, `Results`, `Payments`. It includes primary and foreign keys,
`UNIQUE` / `CHECK` / `DEFAULT` constraints, and seed data. To run it, open the file in SSMS connected
to any SQL Server instance and execute the whole script.

The Part 1 workflow, `.github/workflows/validate-docs.yml`, checks that the planning documents are present.
