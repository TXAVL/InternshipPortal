# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

- **Primary audience: the grading committee (hội đồng chấm) watching a live demo.** The portal is a student course/graduation project; success is the committee understanding each workflow quickly and seeing it work end to end. Each role signs in with a real seeded account, so the committee can see the permission differences first-hand.
- **Roles (illustrative workflows, not separately researched user groups):**
  - **Admin / Khoa (faculty office):** manages everything, approves partner companies, reads the statistics dashboard, exports applicant lists to Excel, adds feedback to weekly reports.
  - **Doanh nghiệp (company):** posts internship openings, reviews applicants and moves them through statuses, comments on weekly reports. Sees only its own postings and applicants.
  - **Sinh viên (student):** browses openings, applies with a CV, sees their own application status in the student portal, submits weekly internship logs once accepted. Can self-register.

## Product Purpose

A faculty-run internship portal that connects students with partner companies and follows the internship past the hiring step: job posting → application with CV → status review (Pending / Interview / Accepted / Rejected) → weekly internship log with mentor/lecturer feedback → faculty-level statistics and Excel reporting.

Success for the project: every role's workflow can be shown live after a quick login, with real numbers drawn from the database, and nothing from earlier milestones (CRUD, CV upload, Excel export) breaks.

## Positioning

Unlike a generic job board, the portal carries the student from application through the internship itself (weekly logs with lecturer feedback) and gives the faculty a single view of placement rate across companies.

## Operating Context

- Shown in front of a committee, typically on a projected laptop screen, driven by a team member signing in as each role during the demo.
- Team workflow: several members work on separate branches (`feature/day-N-…`; Day 2 UI is `feature/day-2-auth-ui`); teammates who own the UI ("Bạn A") submit via `submit-work*.bat` scripts that build Release and open a PR. Backend/auth is delivered as a functional skeleton; visual polish of Auth, Student Portal and Dashboard belongs to Bạn A.
- Interface language is Vietnamese.

## Capabilities and Constraints

- **Stack (binding):** ASP.NET Core MVC on .NET 8 with Razor views and Bootstrap 5.3 + Bootstrap Icons. No SPA framework and no JS build step.
- Data: EF Core + SQL Server / LocalDB, schema created by `EnsureCreated()` and seeded by `DbInitializer` (no migrations).
- Authentication: ASP.NET Core Cookie Authentication against a `UserAccounts` table (passwords hashed with `PasswordHasher<UserAccount>`; no ASP.NET Identity stores). Roles are `Admin`, `Company`, `Student`; the `RelatedId` claim holds the CompanyId or StudentProfileId. No quick-login or role-switch buttons. Authorization uses `[Authorize(Roles = ...)]`; Company users are filtered to their own jobs and applicants.
- Students self-register at `/Account/Register` (creates StudentProfile + UserAccount).
- Excel export via ClosedXML.
- Domain terms: Tài khoản (UserAccount), Mã SV (student code), Tin tuyển dụng (job post), Hồ sơ ứng tuyển (application), Trúng tuyển (Accepted), Nhật ký thực tập / báo cáo tuần (weekly report), GVHD (supervising lecturer).

## Brand Commitments

- Product name currently shown as "INTERN PORTAL".
- **Open decision:** whether a specific university/faculty name or logo should appear. Do not invent one.

## Evidence on Hand

- Seed data only: FPT Software, Viettel Solutions and TXA Studio with four internship openings, and three accounts (shared demo password `123456`): `admin@tdu.vn` (Admin), `hr@txastudio.click` (Company, TXA Studio), `test` (Student, accepted at TXA Studio with a week-1 report and mentor feedback). These are demo fixtures, not real partnerships; do not present them as endorsements, and do not fabricate testimonials, partner counts, or placement statistics beyond what the database returns.

## Product Principles

1. **Demo-legible first:** every screen should make its workflow and its current state obvious to someone seeing it for the first time.
2. **Real numbers only:** statistics and counts come from the database, never hard-coded.
3. **Don't break earlier milestones:** new features layer on top of the existing CRUD, application, and export flows.
4. **Simple to maintain by a student team:** prefer plain Razor + Bootstrap patterns teammates can edit without new tooling.
