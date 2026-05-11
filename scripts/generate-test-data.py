#!/usr/bin/env python3
"""
CRM Platform — Test Data Generator
Generates realistic CRM test data for E2E testing.
Outputs SQL INSERT statements and JSON payloads for API testing.

Usage:
  python3 scripts/generate-test-data.py --tenants 3 --leads 1000 --cases 500
  python3 scripts/generate-test-data.py --format json --output test-data.json
  python3 scripts/generate-test-data.py --format sql --output seed-test-data.sql
"""

import argparse, json, random, uuid, sys
from datetime import datetime, timedelta

# ─── Configuration ────────────────────────────────────────────────────────────
FIRST_NAMES = ["James","Emma","Michael","Sophia","William","Olivia","Benjamin","Ava","Lucas","Isabella",
               "Ethan","Mia","Alexander","Charlotte","Daniel","Amelia","Henry","Harper","Matthew","Evelyn"]
LAST_NAMES  = ["Smith","Johnson","Williams","Brown","Jones","Garcia","Miller","Davis","Rodriguez","Martinez",
               "Anderson","Taylor","Thomas","Moore","Jackson","Martin","Lee","Thompson","White","Harris"]
COMPANIES   = ["Acme Corp","Globex Inc","Initech","Umbrella Co","Stark Industries","Wayne Enterprises",
               "Oscorp","Massive Dynamic","Soylent Corp","Cyberdyne Systems","Aperture Science","Black Mesa"]
INDUSTRIES  = ["Technology","Healthcare","Finance","Manufacturing","Retail","Education","Energy","Transport"]
CASE_SUBJECTS = [
    "Unable to log in to the portal", "Billing discrepancy on last invoice",
    "Feature request: dark mode for dashboard", "Data export not completing",
    "Integration with Salesforce failing", "Account locked after password reset",
    "Missing data in quarterly report", "API rate limit exceeded",
    "Custom field not saving", "Email notification not received"
]
PRODUCTS    = ["CRM Enterprise","CRM Professional","CRM Starter","Analytics Add-on","AI Assistant","API Access"]
CITIES      = ["New York","London","Tokyo","Sydney","Singapore","Berlin","Toronto","Dubai","Mumbai","Paris"]

# ─── Helpers ──────────────────────────────────────────────────────────────────
def rand_date(days_back=365):
    return (datetime.utcnow() - timedelta(days=random.randint(0, days_back))).isoformat() + "Z"

def rand_future_date(days_ahead=90):
    return (datetime.utcnow() + timedelta(days=random.randint(1, days_ahead))).isoformat() + "Z"

def rand_phone():
    return f"+1{random.randint(200,999)}{random.randint(200,999)}{random.randint(1000,9999)}"

def guid(): return str(uuid.uuid4()).upper()

# ─── Generators ───────────────────────────────────────────────────────────────

def generate_tenants(count=3):
    tenants = []
    for i in range(count):
        name = COMPANIES[i]
        tenants.append({
            "id": guid(), "name": name, "domain": name.lower().replace(" ","") + ".com",
            "plan": random.choice(["starter","professional","enterprise"]),
            "createdAt": rand_date(365), "isActive": True
        })
    return tenants

def generate_users(tenants, count_per_tenant=5):
    users = []
    roles = ["SalesRep","SupportAgent","MarketingManager","TenantAdmin","Analyst"]
    for t in tenants:
        for i in range(count_per_tenant):
            fn, ln = random.choice(FIRST_NAMES), random.choice(LAST_NAMES)
            users.append({
                "id": guid(), "tenantId": t["id"],
                "email": f"{fn.lower()}.{ln.lower()}@{t['domain']}",
                "displayName": f"{fn} {ln}",
                "role": roles[i % len(roles)],
                "createdAt": rand_date(180)
            })
    return users

def generate_leads(tenants, users, count=1000):
    leads = []
    sources = ["Web","Referral","Campaign","InboundCall","Partner","Other"]
    statuses = ["New","Contacted","Qualified","Converted","Disqualified"]
    for _ in range(count):
        t = random.choice(tenants)
        u = random.choice([x for x in users if x["tenantId"] == t["id"]])
        leads.append({
            "id": guid(), "tenantId": t["id"],
            "firstName": random.choice(FIRST_NAMES), "lastName": random.choice(LAST_NAMES),
            "jobTitle": random.choice(["CEO","CTO","VP Sales","Director","Manager","Engineer"]),
            "email": f"lead{random.randint(1,99999)}@example.com",
            "phone": rand_phone(), "company": random.choice(COMPANIES),
            "source": random.choice(sources), "status": random.choice(statuses),
            "score": random.randint(0, 100),
            "assignedToUserId": u["id"],
            "isConverted": False, "createdAt": rand_date(90), "updatedAt": rand_date(30)
        })
    return leads

def generate_contacts(tenants, count=500):
    contacts = []
    for _ in range(count):
        t = random.choice(tenants)
        fn, ln = random.choice(FIRST_NAMES), random.choice(LAST_NAMES)
        contacts.append({
            "id": guid(), "tenantId": t["id"],
            "firstName": fn, "lastName": ln,
            "email": f"{fn.lower()}.{ln.lower()}@example.com",
            "phone": rand_phone(), "jobTitle": random.choice(["CEO","CTO","VP","Director","Manager"]),
            "createdAt": rand_date(365)
        })
    return contacts

def generate_accounts(tenants, count=200):
    accounts = []
    for _ in range(count):
        t = random.choice(tenants)
        name = random.choice(COMPANIES) + " " + random.choice(["Ltd","Inc","LLC","Group","Corp"])
        accounts.append({
            "id": guid(), "tenantId": t["id"],
            "name": name, "industry": random.choice(INDUSTRIES),
            "employeeCount": random.choice([5,25,75,200,500,2000,10000]),
            "phone": rand_phone(),
            "annualRevenue": random.choice([50000,250000,1000000,5000000,25000000,100000000]),
            "website": f"https://{name.lower().replace(' ','')}.com",
            "createdAt": rand_date(730)
        })
    return accounts

def generate_opportunities(tenants, users, accounts, contacts, count=300):
    opps = []
    stages = ["Prospecting","Qualification","Proposal","Negotiation","ClosedWon","ClosedLost"]
    for _ in range(count):
        t = random.choice(tenants)
        a = random.choice([x for x in accounts if x["tenantId"] == t["id"]])
        c = random.choice([x for x in contacts if x["tenantId"] == t["id"]] + [None])
        u = random.choice([x for x in users if x["tenantId"] == t["id"]])
        stage = random.choice(stages)
        opps.append({
            "id": guid(), "tenantId": t["id"],
            "title": f"{random.choice(PRODUCTS)} - {random.choice(CITIES)}",
            "stage": stage,
            "amount": random.choice([5000,10000,25000,50000,100000,250000]),
            "probability": {"Prospecting":10,"Qualification":25,"Proposal":50,"Negotiation":75,"ClosedWon":100,"ClosedLost":0}[stage],
            "closeDate": rand_future_date(90) if stage not in ["ClosedWon","ClosedLost"] else rand_date(60),
            "accountId": a["id"], "contactId": c["id"] if c else None,
            "assignedToUserId": u["id"],
            "createdAt": rand_date(180)
        })
    return opps

def generate_cases(tenants, users, contacts, count=500):
    cases = []
    statuses = ["New","Open","Pending","Escalated","Resolved","Closed"]
    priorities = ["Low","Medium","High","Critical"]
    for _ in range(count):
        t = random.choice(tenants)
        c = random.choice([x for x in contacts if x["tenantId"] == t["id"]] + [None])
        u = random.choice([x for x in users if x["tenantId"] == t["id"]])
        status = random.choice(statuses)
        cases.append({
            "id": guid(), "tenantId": t["id"],
            "subject": random.choice(CASE_SUBJECTS),
            "description": f"Customer reported: {random.choice(CASE_SUBJECTS)}. Additional details: ...",
            "status": status, "priority": random.choice(priorities),
            "channel": random.choice(["Email","Phone","Portal","Chat","Api"]),
            "contactId": c["id"] if c else None,
            "assignedToUserId": u["id"] if status not in ["New","Closed"] else None,
            "slaBreached": status in ["Escalated"] and random.random() > 0.7,
            "sentiment": random.choice(["Positive","Neutral","Negative","Mixed"]) if status in ["Resolved","Closed"] else None,
            "createdAt": rand_date(90), "resolvedAt": rand_date(30) if status in ["Resolved","Closed"] else None
        })
    return cases

def generate_campaigns(tenants, users, count=50):
    campaigns = []
    statuses = ["Draft","Scheduled","Active","Paused","Completed","Cancelled"]
    for _ in range(count):
        t = random.choice(tenants)
        u = random.choice([x for x in users if x["tenantId"] == t["id"]])
        campaigns.append({
            "id": guid(), "tenantId": t["id"],
            "name": f"Campaign - {random.choice(CITIES)} {random.randint(1,99)}",
            "channel": random.choice(["Email","Sms","InApp","Push"]),
            "status": random.choice(statuses),
            "impressions": random.randint(0, 50000),
            "clicks": random.randint(0, 5000),
            "conversions": random.randint(0, 500),
            "startDate": rand_date(60), "createdAt": rand_date(90)
        })
    return campaigns

# ─── Output Formatters ────────────────────────────────────────────────────────

def to_api_payloads(data):
    """Generate JSON API request bodies for creating each entity."""
    payloads = []
    for lead in data.get("leads", [])[:10]:  # first 10 as API examples
        payloads.append({"endpoint": "POST /leads", "body": {
            "firstName": lead["firstName"], "lastName": lead["lastName"],
            "email": lead["email"], "phone": lead["phone"],
            "company": lead["company"], "source": lead["source"]
        }})
    for case in data.get("cases", [])[:10]:
        payloads.append({"endpoint": "POST /cases", "body": {
            "subject": case["subject"], "description": case["description"],
            "priority": case["priority"]
        }})
    return payloads

def to_sql(data):
    """Generate SQL INSERT statements for seeding."""
    sql = ["-- CRM Platform Test Data Seed", f"-- Generated: {datetime.utcnow().isoformat()}", ""]
    for lead in data.get("leads", []):
        sql.append(f"INSERT INTO sfa.Leads (Id, TenantId, FirstName, LastName, Email, Company, Source, Status, Score, AssignedToUserId, CreatedAt, UpdatedAt) VALUES ('{lead['id']}','{lead['tenantId']}','{lead['firstName']}','{lead['lastName']}','{lead['email']}','{lead['company']}','{lead['source']}','{lead['status']}',{lead['score']},'{lead['assignedToUserId']}','{lead['createdAt']}','{lead['updatedAt']}');")
    return "\n".join(sql)

# ─── Main ─────────────────────────────────────────────────────────────────────
def main():
    parser = argparse.ArgumentParser(description="CRM Test Data Generator")
    parser.add_argument("--tenants", type=int, default=3)
    parser.add_argument("--leads", type=int, default=1000)
    parser.add_argument("--contacts", type=int, default=500)
    parser.add_argument("--accounts", type=int, default=200)
    parser.add_argument("--opportunities", type=int, default=300)
    parser.add_argument("--cases", type=int, default=500)
    parser.add_argument("--campaigns", type=int, default=50)
    parser.add_argument("--format", choices=["json","sql","api"], default="json")
    parser.add_argument("--output", type=str, default=None)
    args = parser.parse_args()

    tenants = generate_tenants(args.tenants)
    users = generate_users(tenants)
    contacts = generate_contacts(tenants, args.contacts)
    accounts = generate_accounts(tenants, args.accounts)
    leads = generate_leads(tenants, users, args.leads)
    opportunities = generate_opportunities(tenants, users, accounts, contacts, args.opportunities)
    cases = generate_cases(tenants, users, contacts, args.cases)
    campaigns = generate_campaigns(tenants, users, args.campaigns)

    data = {
        "tenants": tenants, "users": users,
        "leads": leads, "contacts": contacts, "accounts": accounts,
        "opportunities": opportunities, "cases": cases, "campaigns": campaigns,
        "stats": {
            "totalLeads": len(leads), "totalContacts": len(contacts),
            "totalAccounts": len(accounts), "totalOpportunities": len(opportunities),
            "totalCases": len(cases), "totalCampaigns": len(campaigns),
            "totalTenants": len(tenants), "totalUsers": len(users)
        }
    }

    if args.format == "api":
        data = to_api_payloads(data)

    output = json.dumps(data, indent=2) if args.format != "sql" else to_sql(data)

    if args.output:
        with open(args.output, "w") as f:
            f.write(output)
        print(f"Written to {args.output}")
    else:
        print(output)

if __name__ == "__main__":
    main()
