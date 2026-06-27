# HubSpot & SMS Enhancements Plan

## Top-Level Overview

This plan covers six enhancement areas for the PicoERP Blazor Server application:

1. **Pagination** — Add server-side pagination to Deals list and Contacts list pages
2. **Deal → ERP Income** — Add "Add to ERP Income" action on deals (closed-won)
3. **File Attachments via HubSpot API** — Attach files to contacts/deals using HubSpot Files API (no local storage)
4. **Call Logs & SMS Logs** — Populate the existing empty tabs in ContactProfile using IPPanel for SMS
5. **Modern Deal/Contact Profile UI + Line Items** — Redesign detail views, load HubSpot products on line items
6. **Full SMS Integration** — Standalone SMS page: compose, send via IPPanel, auto-save logs to HubSpot notes

All changes are additive. Existing service contracts (IHubSpotService, ISmsService) will be extended as needed.
No database migrations are required except for a new `SmsLog` entity if HubSpot-side logging is insufficient.

---

## Sub-Task 1: Pagination for Deals and Contacts

**Status:** `[ ] pending`

### Intent
`HubSpotDeals.razor` and `Contacts.razor` currently load all records in a single call with no pagination UI. For large HubSpot accounts this causes slow loads and poor UX. Add client-side pagination state and UI pagination controls matching the pattern already used in `Incomes.razor`.

### Expected Outcomes
- Deals list shows page N of M with Previous/Next controls and a page-size selector
- Contacts list shows the same controls
- Navigation between pages fetches only the records needed
- Search/filter resets to page 1 automatically

### Todo List
1. **Deals** — Read `HubSpotDeals.razor` and `IHubSpotService.GetDealsAsync` signature in full
2. Extend `IHubSpotService.GetDealsAsync` (or add `GetDealsPagedAsync`) to accept `PaginationParams` and return `PagedResult<HubSpotDealDto>`
3. Update `HubSpotService.GetDealsAsync` implementation to support skip/take (HubSpot API supports `limit` + `after` cursor — map page number to cursor or fetch-and-slice locally if dataset is manageable)
4. Update `HubSpotDeals.razor`: add `PaginationParams _paging` state, wire page controls (use MudPagination or existing MudTablePager pattern from `Incomes.razor`), reset page on filter change
5. **Contacts** — Same pattern: extend `IHubSpotService` with paged contacts method, update `Contacts.razor` with pagination controls

### Relevant Context
- Pattern to copy: `src/PicoERP.Web/Pages/Incomes.razor` — pagination UI and state management
- Pagination types: `src/PicoERP.Application/Common/PagedResult.cs` and `PaginationParams`
- Target files: `src/PicoERP.Web/Pages/HubSpotDeals.razor`, `src/PicoERP.Web/Pages/Contacts.razor`
- Service: `src/PicoERP.Application/Interfaces/IHubSpotService.cs`, `src/PicoERP.Infrastructure/Services/HubSpotService.cs`

---

## Sub-Task 2: Add HubSpot Deal to ERP Income

**Status:** `[ ] pending`

### Intent
When a deal is closed-won, operators should be able to push it as an income record into the ERP system directly from the deal detail or deal list. This bridges the HubSpot CRM pipeline with the accounting module. The existing `PendingDeal` webhook flow handles automatic approval; this sub-task adds a manual "Add to ERP Income" button available on any deal.

### Expected Outcomes
- A button "افزودن به درآمد ERP" appears on the deal detail page (and optionally the deal list)
- Clicking it opens a pre-filled income dialog with deal amount, deal name as description, and today's date
- On confirm, a new `Income` record is created via `IIncomeService.CreateAsync`
- Success/error snackbar feedback shown
- Button is disabled if the deal amount is 0 or null

### Todo List
1. Read `HubSpotDealDetail.razor` and `IncomeDialog.razor` in full to understand current structure
2. Add an "Add to ERP" button in the deal detail action area
3. Add an inline confirmation dialog (or reuse `IncomeDialog.razor`) pre-populated with:
   - `Amount` = deal amount
   - `Description` = deal name
   - `Date` = today
   - Allow operator to choose `CategoryId` and `FinancialAccountId` before confirming
4. On confirm call `IIncomeService.CreateAsync(dto, currentUser)`
5. Show snackbar: "درآمد با موفقیت در ERP ثبت شد" / error message

### Relevant Context
- Target file: `src/PicoERP.Web/Pages/HubSpotDealDetail.razor`
- Income creation: `src/PicoERP.Web/Pages/IncomeDialog.razor` and `src/PicoERP.Application/Interfaces/IIncomeService.cs`
- Income DTO: `src/PicoERP.Application/DTOs/IncomeDto.cs` — `CreateIncomeDto`
- Deal DTO: `src/PicoERP.Application/DTOs/HubSpotDto.cs` — `HubSpotDealDto`

---

## Sub-Task 3: File Attachments via HubSpot Files API

**Status:** `[ ] pending`

### Intent
Allow operators to attach files to HubSpot contacts and deals. Files must be stored in HubSpot's own Files API (not the local server disk). This keeps attachments within HubSpot, visible in CRM, and avoids server storage concerns.

### Expected Outcomes
- Contact profile page has a "Files" tab (already exists as Tab 5 placeholder) that lists uploaded files and allows new uploads
- Deal detail page has a file section to upload and list attached files
- Files are uploaded to HubSpot via `POST /files/v3/files` (multipart) and associated to the contact/deal via engagement or note
- File list is loaded from HubSpot Files API filtered by the object association
- No files are stored on the local server

### Todo List
1. Research HubSpot Files API v3: `POST /files/v3/files` (upload), `GET /files/v3/files/{fileId}` (metadata), `GET /files/v3/files?folderId=...` (list), association via note body or custom property
2. Add to `IHubSpotService`:
   - `UploadFileAsync(string apiKey, Stream fileStream, string fileName, string mimeType, string folderId = "")` → returns `HubSpotFileDto`
   - `GetFilesForObjectAsync(string apiKey, string objectType, string objectId)` → `List<HubSpotFileDto>`
   - `DeleteFileAsync(string apiKey, string fileId)` → Task
3. Add `HubSpotFileDto` to `HubSpotDto.cs`: `{ Id, Name, Url, Size, CreatedAt, MimeType }`
4. Implement the methods in `HubSpotService.cs`
5. **Contact profile**: Populate the existing "Files" tab — show file list, add MudFileUpload component, call service on upload, call delete on remove
6. **Deal detail**: Add a collapsible "Files" section with the same upload/list/delete UI

### Relevant Context
- HubSpot Files API v3 base URL: `https://api.hubapi.com/files/v3/files`
- Auth: `Authorization: Bearer {apiKey}` (same pattern as rest of HubSpotService)
- Target files: `src/PicoERP.Web/Pages/ContactProfile.razor` (Tab 5 "Files" already exists as empty state), `src/PicoERP.Web/Pages/HubSpotDealDetail.razor`
- Service: `src/PicoERP.Infrastructure/Services/HubSpotService.cs`
- Interface: `src/PicoERP.Application/Interfaces/IHubSpotService.cs`

---

## Sub-Task 4: Call Logs & SMS Logs in Contact Profile

**Status:** `[ ] pending`

### Intent
The ContactProfile page has placeholder tabs for "Calls / Activity" (Tab 3) and "SMS Logs" (Tab 4) but they show empty state messages. Populate them:
- **Call Logs**: Load calls from HubSpot engagements API (call type)
- **SMS Logs**: Load sent SMS history from a local `SmsLog` table (SMS messages sent from PicoERP to this contact's mobile number)

### Expected Outcomes
- Calls tab shows a timeline of call engagements (date, duration, direction, body) fetched from HubSpot
- SMS logs tab shows a list of messages sent to/from this contact (timestamp, direction, message body)
- Both tabs have a loading spinner while fetching
- SMS logs auto-refresh when a new message is sent from the deal/contact page

### Todo List
1. **HubSpot Call Logs**: Add `GetContactCallsAsync(string apiKey, string contactId)` to `IHubSpotService` and implement via `/crm/v3/objects/calls?associations=contacts&associatedObjectId={id}`
2. **SmsLog entity**: Create `SmsLog` domain entity: `{ Id, ContactHsId, ContactPhone, Direction (Sent/Received), Message, SentAt, Status, IPPanelMessageId }`
3. Add `SmsLog` to `AppDbContext`, add EF migration
4. Add `ISmsLogService` interface and `SmsLogService` implementation: `GetLogsForContactAsync(contactPhone)`, `SaveLogAsync(SmsLogDto)`
5. Populate **Calls tab** in `ContactProfile.razor`: call `GetContactCallsAsync`, render timeline cards
6. Populate **SMS Logs tab** in `ContactProfile.razor`: call `ISmsLogService.GetLogsForContactAsync`, render message bubbles
7. Wire auto-save: when SMS is sent (Sub-Task 6), call `ISmsLogService.SaveLogAsync` with direction=Sent

### Relevant Context
- HubSpot Engagements/Calls API: `GET /crm/v3/objects/calls` with filter by contact association
- Target file: `src/PicoERP.Web/Pages/ContactProfile.razor` — tabs at lines ~342–410 (call logs and sms logs placeholders)
- SmsLog table follows `BaseEntity` pattern from `src/PicoERP.Domain/Common/BaseEntity.cs`
- DB context: `src/PicoERP.Infrastructure/Persistence/AppDbContext.cs`
- DI registration: `src/PicoERP.Infrastructure/DependencyInjection.cs`

---

## Sub-Task 5: Modern Deal & Contact Profile UI + Line Items with Products

**Status:** `[ ] pending`

### Intent
The current deal detail and contact profile pages are functional but visually basic. Redesign them with a more polished MudBlazor layout. Additionally, when adding/editing line items on a deal, allow loading HubSpot Product Library items (not just custom entries) alongside the current free-form line items.

### Expected Outcomes
- Deal detail has a modern two-column layout: left side hero card (amount, stage badge, dates) and right side tabs (Line Items, Notes, Files, Activity)
- Contact profile hero banner is refreshed with better visual hierarchy
- Line items section allows either (a) search and select from HubSpot Product Library or (b) enter a custom item
- Products from HubSpot Product Library are loaded via the Products API
- Stage change dropdown on deal detail (inline edit)
- Edit mode for deal fields inline (not just dialog)

### Todo List
1. Read current `HubSpotDealDetail.razor` and `ContactProfile.razor` in full for exact current markup
2. Add `GetProductsAsync(string apiKey)` to `IHubSpotService` → `List<HubSpotProductDto>` from `/crm/v3/objects/products`
3. Add `HubSpotProductDto` to `HubSpotDto.cs`: `{ Id, Name, Price, Sku, Description }`
4. Implement `GetProductsAsync` in `HubSpotService.cs`
5. **Deal detail redesign**: Replace current layout with MudPaper hero + MudTabs (Line Items tab, Notes tab, Files tab). Move existing content into tabs. Keep all existing functionality.
6. **Line items dialog**: Add "Load from Product Library" toggle — when on, show searchable product list (loaded once); when off, show free-form fields. On select, pre-fill name/price/sku from product.
7. **Contact profile redesign**: Improve hero section (gradient card, stat chips), modernize the info grid cards, keep all 5 tabs.
8. Both pages: ensure RTL layout, Persian labels, responsive on mobile

### Relevant Context
- Target files: `src/PicoERP.Web/Pages/HubSpotDealDetail.razor`, `src/PicoERP.Web/Pages/ContactProfile.razor`
- MudBlazor components to use: `MudCard`, `MudTabs`, `MudChip`, `MudBadge`, `MudAvatar`, `MudPaper`, `MudTimeline`
- HubSpot Products API: `GET /crm/v3/objects/products`
- Service interface: `src/PicoERP.Application/Interfaces/IHubSpotService.cs`

---

## Sub-Task 6: Full SMS Integration Page

**Status:** `[ ] pending`

### Intent
Create a dedicated SMS page (route `/sms`) where operators can compose and send SMS messages to contacts via IPPanel, view the history of sent messages, and have outgoing messages automatically logged as HubSpot notes on the contact record. This establishes a two-way-ready SMS workflow.

### Expected Outcomes
- `/sms` page with:
  - Contact search/select (loads from local HubSpot contacts)
  - Message composer with character count
  - Sender number selector (loaded from IPPanel via `ISmsService.GetNumbersAsync`)
  - Optional scheduled send time
  - Send button → calls IPPanel API → logs to local SmsLog → creates HubSpot note on contact
- SMS log list below composer: shows all past messages (most recent first), filterable by contact
- Successful sends show snackbar + update log in real time
- Failed sends show error with retry option
- Settings page already has SMS config (Sms:ApiKey, Sms:Sender); the new page uses those

### Todo List
1. Extend `ISmsService` with `SendToContactAsync(string toPhone, string message, string? fromNumber, DateTime? sendTime)` that returns `Result<string>` (the IPPanel outbox ID)
2. Implement in `SmsService.cs` using the full IPPanel send payload (sending_type, from_number, message, params.recipients, send_time)
3. Create `src/PicoERP.Web/Pages/Sms.razor` (route `/sms`) with:
   - Contact search autocomplete (call `IHubSpotService.SearchContactsAsync`)
   - MudTextField for message body with char counter
   - MudSelect for sender number (loaded via `ISmsService.GetNumbersAsync`)
   - Optional `MudDateTimePicker` for scheduled send
   - Send button with loading state
4. On send success:
   - Call `ISmsLogService.SaveLogAsync` (from Sub-Task 4)
   - Call `IHubSpotService.AddNoteAsync` on the contact with message content
   - Refresh the SMS log list
5. SMS log display: `MudDataGrid` or `MudTable` with contact name, phone, message preview, sent time, status
6. Add "/sms" navigation entry to the sidebar menu (`NavMenu.razor` or equivalent layout)
7. Register `ISmsLogService` / `SmsLogService` in `DependencyInjection.cs` (if not done in Sub-Task 4)

### Relevant Context
- IPPanel send endpoint: `POST https://edge.ippanel.com/v1/api/send`
- Payload: `{ sending_type: "webservice", from_number, message, params: { recipients: [...] }, send_time? }`
- Auth: `Authorization: {apiKey}` (no Bearer prefix) — already implemented in `SmsService.cs`
- Existing service: `src/PicoERP.Infrastructure/Services/SmsService.cs`
- Interface: `src/PicoERP.Application/Interfaces/ISmsService.cs`
- Contact search: `IHubSpotService.SearchContactsAsync(query)`
- HubSpot note: `IHubSpotService.AddNoteAsync(body, dealId?, contactId?)` 
- SmsLog service from Sub-Task 4
- Nav menu: `src/PicoERP.Web/Shared/NavMenu.razor` (or `Layout/`)

---

## Implementation Order

```
Sub-Task 1 → Pagination (low risk, isolated)
Sub-Task 4 → SmsLog entity + call logs (creates DB entity used by Sub-Tasks 5 & 6)
Sub-Task 2 → Deal → ERP Income button (uses existing services)
Sub-Task 3 → HubSpot File Attachments (new API methods)
Sub-Task 5 → Modern UI + Products (depends on Sub-Tasks 3 & 4 for full tabs)
Sub-Task 6 → Full SMS Page (depends on Sub-Task 4 for SmsLog)
```
