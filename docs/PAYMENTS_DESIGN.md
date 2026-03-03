# Payments – One Place for All Money In & Out

This document suggests how to **record and view all payment details in one place**: money received from clients, and money paid to hotels, houseboats, drivers, employees, and other office expenditure. It fits your current **Accounts** and **Vendor Management** setup and keeps one ledger.

---

## 1. Current State (What You Have)

| Area | What it does | Data source |
|------|----------------|-------------|
| **Accounts** (`/accounts`) | Record and list payments. **Received** = from client (Lead, optional Package). **Made** = to Hotel/Houseboat OR to Transport company only. | `Payment` table, `api/payments` |
| **Vendor Management** | **Vendor Summary** and **Hotel/Transport Payables**: show *Payable* (from package/day itineraries) vs *Paid* (from `Payment` where Made + HotelId or TransportCompanyId). | Same `Payment` table for "Paid"; payables from `DayItineraries` / package |
| **Employee Compensation** (Employee Management) | Salary, incentive, bonus per employee. | `EmployeeSalary` table (separate from `Payment`) |

So today:
- **Received** and **Made (Hotel/Houseboat/Transport)** are already in one place: **Accounts**.
- **Made** does **not** cover: employees, drivers (as a distinct payee), or office/other expenditure.
- Vendor reports already use the same `Payment` records for "paid"; they just need to stay linked.

---

## 2. Recommended Design: One “Payments” Centre

**Idea:** Treat **Accounts** as the single **Payments** (or **Finance**) module: one list, one form, one API. Extend “Payment made” so it can be to **Hotel, Houseboat, Transport, Employee, Driver, Office/Other**. Vendor Management stays as **reports and views by vendor**, with links to “Record payment” that open the same payment form (vendor pre-filled).

Result:
- **One place** to record: client receipts + all outflows (hotel, houseboat, transport, employee, driver, office).
- **One list** with filters: direction (Received / Made), category (e.g. Client, Hotel, Houseboat, Transport, Employee, Driver, Office), date range.
- **Vendor Management** = vendor-focused reports (payable vs paid) + “Record payment” → same payment form.
- **Employee compensation** can either stay as-is (salary/incentive/bonus entries) and **also** allow “Payment made to Employee” in the ledger for actual disbursement, or you show compensation rows in the same list (unified view). See section 4.

---

## 3. Data Model Change: Extend “Payment Made”

Today for **Made** you have:
- `HotelId` (hotel or houseboat)
- `TransportCompanyId`

Validation: exactly one of them required.

**Proposed extension:** Add a **payee category** for **Made** so you can support employees, drivers, and office/other without overloading FKs.

### Option A – Add `PaymentPayeeCategory` (recommended)

- New enum, e.g.:
  - `VendorHotel`, `VendorHouseboat`, `VendorTransport` (current behaviour: use HotelId or TransportCompanyId).
  - `Employee` (use new nullable `UserId`).
  - `Driver` (optional: link to TransportCompanyId + type “driver”, or free text).
  - `OfficeOther` (free text: `PayeeDescription` or `Notes`).

- **Payment** entity (add):
  - `PaymentPayeeCategory? PayeeCategory` (null when `PaymentType == Received`).
  - `Guid? UserId` (for Employee; FK to User).
  - `string? PayeeDescription` (for Driver/Office/Other when you don’t use a vendor FK).

- **Validation for Made:**
  - If category is Hotel/Houseboat → require `HotelId`.
  - If category is Transport (or Driver if you use TransportCompany) → require `TransportCompanyId`.
  - If category is Employee → require `UserId`.
  - If category is OfficeOther (and Driver if not linked to transport) → require or allow `PayeeDescription`.

So:
- **Received**: unchanged (LeadId, optional PackageId).
- **Made**: always one category; one of HotelId, TransportCompanyId, UserId, or PayeeDescription is set accordingly.

This gives you **one table**, one API, one list, with filters by category.

### Option B – Keep only FKs and “Other”

- Add to **Payment**: `UserId?` (employee), `string? PayeeDescription` (for driver/office/other).
- Validation for Made: require **at least one** of HotelId, TransportCompanyId, UserId, or (if you allow) “other” when PayeeDescription is set.
- No enum; category is derived in UI/API from which FK is non-null (and “Other” if only PayeeDescription).

Option A is clearer for reporting and filters (“show all Employee payments”, “show all Office expenditure”).

---

## 4. Employee Compensation vs Payment

You have two options:

**A) Two layers (recommended for clarity)**  
- **Employee compensation** (Employee Management): salary/incentive/bonus **entries** (what you owe or have allocated) – keep `EmployeeSalary` as is.  
- **Payment (Accounts)**: actual **disbursement** – “Payment made to Employee” with category Employee and `UserId`. So you can record “paid salary to X on date Y” in the same ledger as client receipts and vendor payments. Compensation module can optionally have “Record payment” that creates a Payment with category Employee and pre-filled UserId/amount/date.

**B) Single source**  
- Treat EmployeeSalary as the only record of “payment to employee”. Then in the **unified Payments list**, you either:
  - **Aggregate**: show `Payment` rows + a **virtual** list of EmployeeSalary rows (read-only in the list, “from compensation”), or  
  - **Sync**: when you add compensation, also create a Payment (Made, Employee, UserId). Then one table holds everything.

Recommendation: **A** – keep compensation for “what we pay” and use Payment for “money actually moved”; both can appear in one view by querying Payment + optionally EmployeeSalary for display.

---

## 5. UI / Navigation

- **Rename “Accounts” → “Payments” (or “Finance”)** in the menu and routes (e.g. `/payments`). Same module key (Accounts) or a new one if you prefer.
- **Single list page**: filters = Direction (Received / Made), Category (Client, Hotel, Houseboat, Transport, Employee, Driver, Office/Other), Date from/to, optional Lead/Vendor/User. One “Add payment” button.
- **Single form**: first choose Received vs Made. If Made, choose category (Hotel, Houseboat, Transport, Employee, Driver, Office/Other); then show the right fields (e.g. Hotel dropdown, Transport dropdown, User dropdown, or free text).
- **Vendor Management** unchanged as a **hub** with:
  - Vendor Summary  
  - Hotel/Houseboat Payables  
  - Transport Payables  
  - “Payments to Vendors” → can be a **link to Payments with a pre-filled filter** (e.g. “Made” + “Hotel/Transport”) or a deep link to “Add payment” with vendor type pre-selected. Under the hood it’s still the same Payment form and API.

So: **one place to record and list everything**; Vendor Management is the **vendor-facing view** and entry point for recording vendor payments.

---

## 6. Implementation Order

1. **Backend**
   - Add enum `PaymentPayeeCategory` (e.g. VendorHotel, VendorHouseboat, VendorTransport, Employee, Driver, OfficeOther).
   - Add to `Payment`: `PayeeCategory?`, `UserId?`, `PayeeDescription?` (and migration).
   - Update `PaymentsController`: create/update/list to accept and return category, UserId, PayeeDescription; validation as above.
   - Optional: endpoint or query to “unified list” that can also include EmployeeSalary rows if you want one combined view.

2. **Frontend**
   - Rename Accounts → Payments (menu, routes, titles). Keep or reassign module key.
   - Payment form: when type = Made, add category selector; show HotelId / TransportCompanyId / UserId / PayeeDescription by category.
   - List: add category filter; show category (and payee name) in the table.

3. **Vendor Management**
   - “Payments to Vendors” → navigate to `/payments` (or `/payments/create`) with Made + vendor category pre-selected (or open form with vendor type from query params).

4. **Optional**
   - Summary widget on Payments (or Dashboard): Total Received, Total Made by category, “balance” (e.g. received − made) in a period.
   - From Employee Compensation: “Record payment” button that creates a Payment (Made, Employee, UserId) and optionally links to or updates compensation.

---

## 7. Summary

| Goal | Approach |
|------|----------|
| All payment details in one place | One **Payments** (Accounts) module: one list, one form, one API. |
| Received from clients | Unchanged: LeadId, optional PackageId. |
| Paid to hotel, houseboat, transport | Unchanged: HotelId / TransportCompanyId; add PayeeCategory for clarity. |
| Paid to employees | Add category Employee + UserId; keep EmployeeSalary for compensation entries; optionally “Record payment” from compensation. |
| Paid to drivers / office / other | Add category Driver and OfficeOther + PayeeDescription (and optional FKs if needed). |
| Vendor Management | Keep as reports; “Payments to Vendors” = link to same Payments (form/list) with vendor pre-selected. |

This gives you **one place** to record and view all payments while keeping Vendor Management as the vendor-focused report and entry point.
