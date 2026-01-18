# CivicService Project Analysis

## Current State Summary
You have a solid foundation: REST API, proper layering, pagination, filtering, stats dashboard, map view. But it's missing several features that real city systems have.

---

## High-Impact Improvements

### 1. Add a Proper Workflow & Assignment System
Real city systems don't just have Open/InProgress/Closed. They have:

```
Submitted → Triaged → Assigned → InProgress → PendingInspection → Resolved → Closed
```

Plus:
- **Priority levels** (Emergency, High, Normal, Low)
- **SLA tracking** (Service Level Agreements - "Potholes must be fixed within 5 business days")
- **Department assignment** (Roads, Parks, Water, etc.)
- **Worker/crew assignment**

### 2. Service Request Types with SLA Configuration
Calgary has specific response times per category. You could add a `ServiceType` configuration table:

| Category | Target Response (hrs) | Department |
|----------|----------------------|------------|
| Pothole | 48 | Roads |
| Water Leak | 4 | Water Services |
| Street Light | 72 | Transportation |

Then track SLA compliance in your dashboard.

### 3. Add Comments/Activity History
Every real 311 system has an activity log:
- "Request created by citizen"
- "Assigned to Roads Department"
- "Comment from worker: Scheduled for Tuesday"
- "Status changed to Resolved"
- "Citizen feedback: Issue not fully resolved"

### 4. Authentication & Roles
This is a big one. Add:
- **Citizen role**: Can submit requests, view their own requests, add comments
- **Employee role**: Can view all requests in their department, update status, add internal notes
- **Admin role**: Full access, can reassign, view all stats

Even a simple JWT-based auth would show you understand real-world requirements.

### 5. Tracking Number System
Calgary uses tracking numbers like `SR-2024-00001234`. More user-friendly than GUIDs.

### 6. Attachments/Photos
Citizens should be able to upload photos of the issue. This is standard in 311 apps.

---

## Calgary-Specific Suggestions

Looking at Calgary's actual 311 categories, you could align yours:

| Your Current | Calgary Equivalent |
|--------------|-------------------|
| Pothole | Road Maintenance |
| StreetLight | Traffic Signal/Street Light |
| Graffiti | Graffiti Removal |
| IllegalDumping | Illegal Dumping |
| SidewalkRepair | Sidewalk Maintenance |
| TreeMaintenance | Tree Maintenance |
| WaterLeak | Water Emergency |

Add more Calgary-specific ones:
- **Snow/Ice Removal**
- **Park Maintenance**
- **Bylaw Complaint**
- **Transit Shelter**
- **Drainage Issue**

---

## Quick Wins (Easier to Implement)

1. **Add a `TrackingNumber` field** with format `SR-YYYY-NNNNNN`
2. **Add `Priority` enum** (Emergency, High, Normal, Low)
3. **Add `Department` enum** and auto-assign based on category
4. **Add `DueDate` calculated from SLA**
5. **Add a `RequestHistory` table** for audit trail
6. **Add request search** by tracking number, address, or description
7. **Add a public status lookup page** ("Check your request status")

---

## Architecture Improvements

1. **API Versioning** (`/api/v1/requests`) - shows you think about backwards compatibility
2. **Proper error responses** with problem details (RFC 7807)
3. **Request validation middleware** instead of just DataAnnotations
4. **Health check endpoint** (`/health`)
5. **Rate limiting** to prevent abuse
6. **CORS configuration** for real deployments

---

## Suggested Implementation Order

### Phase 1: Core Enhancements
1. Tracking numbers + Priority + Department (schema changes)
2. Request history/audit trail (new table)
3. SLA configuration and due date tracking

### Phase 2: Authentication
1. Basic JWT authentication with roles
2. User management (Citizen, Employee, Admin)
3. Role-based endpoint access

### Phase 3: Advanced Features
1. File attachments for photos
2. Comments system
3. Email/SMS notifications
4. Full-text search
