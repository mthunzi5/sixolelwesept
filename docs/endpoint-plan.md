# RaceDay API Endpoint Plan (Section B)

Conventions used throughout this table:

- **Role Required** — `None` (public, no token needed), `Any` (any logged-in user, either role), `Organiser`, or `Participant`. Where a row says *(owner)*, the caller must additionally be the Organiser who created the Event/Category, or the Participant who owns the Enrolment — this is enforced in the handler, not just by role.
- **Request Body** — JSON fields sent in the body. `None` for GET/DELETE endpoints that carry no payload.
- Authentication is via a JWT bearer token issued by `/api/auth/login`, sent as `Authorization: Bearer <token>`. The token carries `UserID` and `Role`, which is how "owner" checks and `me` routes resolve the caller without a body field.

| HTTP Method | Route | Description | Role Required | Request Body | Expected Response |
|---|---|---|---|---|---|
| **Authentication** | | | | | |
| POST | `/api/auth/register` | Creates a new account as either an Organiser or a Participant. | None | `{ fullName, email, password, role, phoneNumber }` | 201 Created — new user (no password hash) · 400 Bad Request — validation failed · 409 Conflict — email already registered |
| POST | `/api/auth/login` | Verifies credentials and issues a JWT for subsequent requests. | None | `{ email, password }` | 200 OK — `{ token, user }` · 401 Unauthorized — invalid credentials |
| **User Profile** | | | | | |
| GET | `/api/users/me` | Returns the logged-in user's own profile. | Any | None | 200 OK — user profile · 401 Unauthorized |
| PUT | `/api/users/me` | Updates the logged-in user's own profile fields. | Any | `{ fullName, phoneNumber }` | 200 OK — updated profile · 400 Bad Request |
| PUT | `/api/users/me/password` | Changes the logged-in user's password. | Any | `{ currentPassword, newPassword }` | 200 OK · 400 Bad Request — weak/mismatched password · 401 Unauthorized — current password wrong |
| GET | `/api/users/{id}` | Returns another user's public profile (e.g. an organiser viewing a participant they share an event with). | Any | None | 200 OK — public profile fields · 404 Not Found |
| **Events** | | | | | |
| GET | `/api/events` | Lists events, optionally filtered by `status` or date range query params. | None | None | 200 OK — array of events |
| GET | `/api/events/{id}` | Returns a single event with its categories. | None | None | 200 OK — event + categories · 404 Not Found |
| GET | `/api/organisers/me/events` | Lists events created by the logged-in organiser. | Organiser | None | 200 OK — array of events |
| POST | `/api/events` | Creates a new event owned by the logged-in organiser. | Organiser | `{ eventName, eventDate, location, description }` | 201 Created — new event · 400 Bad Request |
| PUT | `/api/events/{id}` | Updates an event's details or status. | Organiser (owner) | `{ eventName, eventDate, location, description, status }` | 200 OK — updated event · 400 Bad Request · 403 Forbidden — not the owner · 404 Not Found |
| DELETE | `/api/events/{id}` | Cancels/removes an event. | Organiser (owner) | None | 204 No Content · 403 Forbidden · 404 Not Found · 409 Conflict — event already has enrolments |
| **Categories** | | | | | |
| GET | `/api/events/{id}/categories` | Lists the race categories (e.g. 5 km, 10 km) for an event. | None | None | 200 OK — array of categories · 404 Not Found |
| GET | `/api/categories/{id}` | Returns a single category's details. | None | None | 200 OK — category · 404 Not Found |
| POST | `/api/events/{id}/categories` | Adds a new category to an event. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants }` | 201 Created — new category · 400 Bad Request · 403 Forbidden · 404 Not Found — event doesn't exist |
| PUT | `/api/categories/{id}` | Updates a category's details. | Organiser (owner) | `{ categoryName, distanceKm, entryFee, maxParticipants }` | 200 OK — updated category · 400 Bad Request · 403 Forbidden · 404 Not Found |
| DELETE | `/api/categories/{id}` | Removes a category. | Organiser (owner) | None | 204 No Content · 403 Forbidden · 404 Not Found · 409 Conflict — category already has enrolments |
| **Event Enrolments** | | | | | |
| POST | `/api/categories/{id}/enrolments` | Enrols the logged-in participant into a category and assigns a bib number. | Participant | None *(participant identity comes from the token)* | 201 Created — enrolment with bib number · 404 Not Found — category doesn't exist · 409 Conflict — already enrolled, or category full |
| GET | `/api/participants/me/enrolments` | Lists the logged-in participant's own enrolments. | Participant | None | 200 OK — array of enrolments |
| GET | `/api/categories/{id}/enrolments` | Lists everyone enrolled in a category (the organiser's roster). | Organiser (owner) | None | 200 OK — array of enrolments · 403 Forbidden · 404 Not Found |
| GET | `/api/enrolments/{id}` | Returns a single enrolment's detail. | Participant (owner) / Organiser (owner) | None | 200 OK — enrolment · 403 Forbidden · 404 Not Found |
| DELETE | `/api/enrolments/{id}` | Withdraws/cancels an enrolment. | Participant (owner) / Organiser (owner) | None | 204 No Content · 403 Forbidden · 404 Not Found · 409 Conflict — a result has already been published |
| **Results** | | | | | |
| GET | `/api/events/{id}/results` | Public leaderboard/results for a finished event. | None | None | 200 OK — array of results, ranked by category · 404 Not Found |
| GET | `/api/participants/me/results` | Lists the logged-in participant's own results across events. | Participant | None | 200 OK — array of results |
| POST | `/api/enrolments/{id}/result` | Captures the finishing result for an enrolment. | Organiser (owner) | `{ finishTimeSeconds, position, status }` | 201 Created — new result · 400 Bad Request · 403 Forbidden · 404 Not Found · 409 Conflict — result already captured (use PUT) |
| PUT | `/api/results/{id}` | Corrects an already-captured result. | Organiser (owner) | `{ finishTimeSeconds, position, status }` | 200 OK — updated result · 400 Bad Request · 403 Forbidden · 404 Not Found |
| **Payments** *(supports the entry-fee flow implied by `Categories.EntryFee`)* | | | | | |
| POST | `/api/enrolments/{id}/payment` | Records payment of the entry fee for an enrolment. | Participant (owner) | `{ amount, paymentMethod }` | 201 Created — payment record · 400 Bad Request · 403 Forbidden · 404 Not Found · 409 Conflict — already paid |
| GET | `/api/enrolments/{id}/payment` | Returns the payment status for an enrolment. | Participant (owner) / Organiser (owner) | None | 200 OK — payment · 403 Forbidden · 404 Not Found |

## Notes on deviations

Anything the implemented API in Part 2 ends up doing differently from this table (route naming, an endpoint split in two, an extra filter param, etc.) will be explained in the main `README.md`, per the Section B requirement that unexplained deviations affect the mark.

**Part 2 update:** the implemented API is on the `part2-api` branch. Every difference from this plan
(JWT replaced by session authentication, added event and category fields, and three extra endpoints) is
listed with its reason in the README section "Differences from the Part 1 plan".
