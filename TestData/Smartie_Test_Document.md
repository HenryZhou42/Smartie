# Smartie Test Document

This document is a **controlled fixture** for validating Smartie Knowledge Base behavior, document-aware chat, and future retrieval features. It contains structured facts, tables, and code that automated and manual tests can reference with predictable answers.

**Document ID (fixture):** `SMARTIE-TEST-001`  
**Effective date:** January 1, 2026  
**Version:** 1.0  
**Classification:** Internal — Test Data Only

---

## Employee Information

The following profile represents a sample employee record used in Smartie integration tests.

| Field | Value |
|-------|-------|
| Full name | Alex Rivera |
| Employee ID | EMP-10482 |
| Department | Engineering |
| Job title | Senior Software Developer |
| Start date | March 15, 2021 |
| Employment type | Full-time, permanent |
| Office location | Toronto, Canada |
| Manager | Sarah Johnson (Engineering Manager) |

Alex Rivera is eligible for all standard benefits described in this document. For HR inquiries, contact **Michael Lee** (HR Representative).

---

## Compensation

Compensation for test employees follows a simplified band model. Values below are **annual base salary in CAD** before bonuses and equity.

| Level | Base salary range (CAD) | Bonus target |
|-------|-------------------------|--------------|
| Junior Developer | $75,000 – $95,000 | 5% |
| Intermediate Developer | $95,000 – $120,000 | 8% |
| Senior Developer | $120,000 – $145,000 | 10% |
| Staff Engineer | $145,000 – $175,000 | 12% |

**Sample employee (Alex Rivera):** Senior Developer band, base salary **$132,000 CAD** per year, with a **10%** annual bonus target.

Salary reviews occur each **April**. Merit increases typically range from **2% to 8%** depending on performance rating.

---

## Vacation Policy

Smartie Test Corp accrues vacation based on years of continuous service. The policy below is the authoritative source for vacation-day questions in test scenarios.

### Accrual tiers

| Years of service | Annual vacation days |
|------------------|----------------------|
| First 3 years | **15 days** |
| After 3 years | **20 days** |
| After 10 years | **25 days** |

### Additional rules

- Vacation accrues monthly; employees may carry forward up to **5 unused days** into the next calendar year.
- Unused days beyond the carry-forward limit are forfeited on **December 31**.
- New hires receive a prorated allocation based on start date.
- Public holidays are **not** deducted from vacation balances.

**Citation anchor:** When asked "How many vacation days?", answers must reference the tier table above and the employee's tenure (Alex Rivera, started March 2021, qualifies for **20 days** as of 2026).

---

## Benefits

Eligible full-time employees receive the following benefits package:

| Benefit | Details |
|---------|---------|
| Health insurance | Extended health and dental, 80% employer-paid |
| Retirement | RRSP matching up to **4%** of base salary |
| Life insurance | 1× annual salary, employer-paid |
| Disability | Short-term and long-term disability coverage |
| Wellness stipend | **$500 CAD** per year for fitness or mental health |
| Parental leave | Up to **16 weeks** paid parental leave |

Benefits enrollment opens during the first **30 days** of employment. Changes outside open enrollment require a qualifying life event.

---

## Remote Work Policy

Smartie Test Corp operates under a **hybrid remote work** model.

- Employees may work remotely up to **3 days per week**.
- At least **2 days per week** must be spent in the office for team collaboration.
- Fully remote arrangements require VP approval and are reviewed annually.
- Core collaboration hours are **10:00 AM – 3:00 PM Eastern Time**.

Remote work equipment (laptop, monitor, headset) is provided by IT. Home internet stipend: **$50 CAD** per month for approved remote employees.

---

## Training Budget

Each employee receives an annual professional development allowance:

- **Amount:** **$2,000 CAD** annually
- **Eligible expenses:** Conferences, certifications, books, online courses, workshops
- **Approval:** Manager approval required for expenses over **$500 CAD**
- **Unused funds:** Do not roll over; reset each **January 1**
- **Reimbursement:** Submit receipts within **30 days** of purchase

Popular approved platforms include Pluralsight, Udemy Business, and Microsoft Learn.

---

## Technology Stack

The Engineering department standardizes on the following technologies for Smartie and related products:

| Layer | Technologies |
|-------|--------------|
| Languages | **C#**, TypeScript, SQL |
| Backend | **ASP.NET Core**, Minimal APIs |
| Data access | **Entity Framework Core** |
| Databases | **SQLite** (local/dev), **SQL Server** (production), **PostgreSQL** (optional cloud) |
| Frontend | Blazor, MAUI |
| AI / ML | Semantic Kernel (planned integrations) |
| Testing | xUnit, bUnit |

When documenting architecture decisions, prefer **Clean Architecture** with separate Domain, Application, Infrastructure, and presentation layers.

---

## Internal Contacts

Use these contacts for test citation scenarios. Names and roles must match exactly.

| Role | Name | Email | Phone |
|------|------|-------|-------|
| Engineering Manager | **Sarah Johnson** | sarah.johnson@smartie-test.example | ext. 4102 |
| HR Representative | **Michael Lee** | michael.lee@smartie-test.example | ext. 2201 |
| IT Support | Help Desk | it-support@smartie-test.example | ext. 1000 |
| Payroll | Finance Team | payroll@smartie-test.example | ext. 3300 |

For urgent HR matters, contact **Michael Lee** during business hours (9 AM – 5 PM ET).

---

## Feature Table

Roadmap status for Smartie Community Edition features. Use this table to validate Markdown table rendering and factual retrieval.

| Feature | Status |
|---------|--------|
| Chat | Complete |
| Dashboard | Complete |
| Knowledge Base | In Progress |
| Document Attachments | In Progress |
| Markdown Rendering | Complete |
| PDF Parsing | Planned |
| Chunking | Planned |
| Embeddings | Planned |
| RAG | Planned |
| Vector Search | Planned |

---

## Code Example

The following C# model mirrors employee fields referenced elsewhere in this document. Use it to validate **code block rendering** and syntax highlighting.

```csharp
public class Employee
{
    public string Name { get; set; }

    public int VacationDays { get; set; }

    public decimal Salary { get; set; }
}
```

Extended example with policy helpers for automated tests:

```csharp
public static class VacationPolicy
{
    public static int GetAnnualDays(int yearsOfService) => yearsOfService switch
    {
        < 3 => 15,
        < 10 => 20,
        _ => 25
    };
}

public record TrainingBudget(decimal AnnualAmountCad, string Currency = "CAD")
{
    public static TrainingBudget Default => new(2000m);
}
```

---

## Frequently Asked Questions

### How many vacation days do I get?

Depends on tenure: **15 days** during the first 3 years, **20 days** after 3 years, and **25 days** after 10 years. See [Vacation Policy](#vacation-policy).

### What is the annual training budget?

**$2,000 CAD** per employee per year. See [Training Budget](#training-budget).

### How many remote days are allowed?

Up to **3 days per week** remote; minimum **2 days** in office. See [Remote Work Policy](#remote-work-policy).

### Who is the engineering manager?

**Sarah Johnson**. See [Internal Contacts](#internal-contacts).

### Who should I contact for HR questions?

**Michael Lee**, HR Representative. See [Internal Contacts](#internal-contacts).

### What databases does the team use?

**SQLite**, **SQL Server**, and **PostgreSQL** (see [Technology Stack](#technology-stack)).

### Is RAG available yet?

No. **RAG**, **embeddings**, **chunking**, and **vector search** are **Planned** per the [Feature Table](#feature-table).

---

## Appendix: Chunking & Embedding Notes (Future RAG)

This section provides dense, section-bounded text for future chunking and embedding pipelines.

**Chunk A — Vacation:** 15/20/25 day tiers; carry forward max 5 days; Alex Rivera → 20 days in 2026.  
**Chunk B — Training:** $2,000 CAD/year; no rollover; manager approval > $500.  
**Chunk C — Remote:** 3 days remote, 2 days office; core hours 10 AM–3 PM ET.  
**Chunk D — Contacts:** Sarah Johnson (Engineering), Michael Lee (HR).  
**Chunk E — Stack:** C#, ASP.NET Core, EF Core, SQLite, SQL Server, PostgreSQL.

When implementing RAG, retrieval queries such as *"vacation days after 3 years"* should return **Chunk A** with highest relevance; *"training budget"* should return **Chunk B**.

---

*End of Smartie Test Document — SMARTIE-TEST-001 v1.0*
