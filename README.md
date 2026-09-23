# Global Procurement Operations Hub

A Unity 6 prototype that turns decentralized purchase requests from a network of marine terminals, rail and intermodal operations, and a terminal-software company into standardized, traceable procurement decisions.

> I studied how Carrix's marine, rail and technology companies create procurement complexity, then built a prototype that converts decentralized requests into standardized, traceable procurement decisions. Built with Unity 6 and C#, using agentic AI as part of the development workflow. I defined the product requirements and architecture, guided implementation, reviewed the generated code, tested behavior, and made the final technical and product decisions.

**All data is synthetic.** Companies, sites, suppliers, contracts, prices and people are generated from a fixed seed for demonstration. This project is not affiliated with or endorsed by Carrix, SSA Marine, Rail Management Services or Tideworks Technology. I built this as a hypothesis based on the role description and my understanding of the organization. I would not assume this is the right production solution without interviewing Procurement users and examining the existing workflow. Although, this was built using Unity and C# just for demo purposes, I was thinking the actual application could utilize a combination ASP.NET, Blazor, Powerautomate, etc.

<img width="1885" height="955" alt="image" src="https://github.com/user-attachments/assets/f7590af2-9f23-4cd4-abd7-6b9bab32b969" />

## Download and launch
1. Download the executable from this link: https://drive.google.com/file/d/1yOh0Lr8qGFXQCmLY7qG8WeNztDIOF50U/view?usp=drive_link
2. Unzip it and launch the "GlobalProcurementHub.exe" file.

Tip: Your work (submitted requests, approvals, merges, policy changes, and the persona you're acting as) is saved to `procurement-hub-state.json` in `Application.persistentDataPath`. To start over, click **Reset demo data** in the sidebar.

## Getting Started
Overview:
1. Drag the 3D globe to see all Carrix subsidariess.
2. Hover over each node to view summary or click them to view comprehensive procurement details - It will display spending/analytics, requests/alerts, categories for that specific division.

New Request:
1. Click new request, it will display a list of scenarios, click one.
2. Select who's opening the request and which entity/site.
3. Write a description of what you are requesting, supplier, and deadline/priority.
4. This request will now show up in the request queue with all the information regarding it. Once the request lifecycle is complete, it will be closed.

Features:
1. Spend analytics - Displayed by business, entities, categories, and timeframe.
2. Suppliers and contracts - View list of current suppliers along with categories and spend data.
3. Data quality - ERP data conversion.
4. Policy and routing - Change and apply policies.
5. Data Model and SQL - View SQL data and export data as CSV.

## What it does

| Screen | Purpose |
|---|---|
| **Command Center** | Dot-matrix 3D globe of about 70 sites. Each column's height shows 12-month spend, and its color shows the business unit. Pulsing rings mark critical or serious alerts. Click a site to see its profile (fleet, ERP status, top categories), with supplier-flow arcs drawn white for on-contract suppliers and amber for off-contract ones. KPIs and a live alert feed let you acknowledge or resolve alerts. |
| **New Request** | Intake form with a **live decision panel**. As you type, the hub classifies the need, evaluates the three engagement rules, routes the request to the category manager, builds the approval chain, suggests existing contracts, and benchmarks the price against network history. Eight one-click scenarios exercise each path. |
| **Request Queue** | Every request from intake to closed PO, with a lifecycle timeline, rule outcomes, stage actions (route, source, approve, receive, 3-way match, close, reject), emergency post-review, and a complete audit trail. |
| **Spend Analytics** | One normalized spend cube across entity, site, category and supplier. Includes monthly spend split into on-contract, off-contract and no-agreement; an entity × category heatmap; top suppliers rolled up across vendor records; and maintenance cost per crane or powered unit, benchmarked across sites. |
| **Suppliers & Contracts** | Golden supplier records with scorecards and the vendor-master fragments behind them (different names, IDs and systems per entity). Contracts show scope, utilization, price list and the leakage around each agreement. |
| **Data Quality** | Fuzzy vendor clustering with human-approved merges, and the legacy → IFS ERP migration over time. Unclassified legacy spend gets auto-classification suggestions, alongside 3-way match exceptions. |
| **Policy & Routing** | Editable engagement rules with a live impact preview on the request backlog, plus the delegation-of-authority matrix and the category → manager routing matrix. |
| **Data Model & SQL** | Relational schema (13 tables) and the PostgreSQL behind each metric, with live result previews. Exports `01_schema.sql`, `02_seed.sql`, `03_analytics_queries.sql` and `purchase_lines.csv`. |

# Conclusion
The Global Procurement Operations Hub is a centralized experience layer designed to make IFS easier to use and act on—not replace it. It provides a guided intake process across departments, proactive alerts for category managers, and accessible spend analysis across locations. The Hub brings requests, suppliers, contracts, purchasing activity, and operational context together so users can identify the correct workflow, determine when Procurement should be involved, and turn fragmented records into actionable work.

I defined the business problem, product requirements, application scope, and user experience before structuring the prototype in Unity and C#. I used coding agents to accelerate selected implementation tasks while supplying the requirements and constraints, reviewing and modifying generated code, testing behavior, debugging integration issues, and retaining responsibility for every final technical and product decision.
