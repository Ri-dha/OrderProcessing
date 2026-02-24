





**BACK-END DEVELOPER**

**TECHNICAL ASSESSMENT**

Build, Prove & Explain  |  7-Day Take-Home  |  10-Min Video Required


Candidate: Ridha Tareq

Position: Back-End Developer (.NET)

CONFIDENTIAL

Back-End Developer Technical Assessment  |  Confidential
# **Assessment Format**
You will build a complete Order Processing Service from scratch. Unlike a typical “build a CRUD API” test, this assessment requires you to prove your system works correctly under pressure and explain your engineering decisions in a short video walkthrough.

## **Three Deliverables**

|**#**|**Deliverable**|**What It Proves**|
| :- | :- | :- |
|1|**Working Code**|You can build production-grade software|
|2|**Test Evidence**|Your system actually works — proven with concrete test output, not just trust|
|3|**Video Walkthrough**|YOU built it and understand every decision (10-min screen recording, max)|

|<p>**Video Walkthrough Is Mandatory**</p><p>You must submit a 10-minute (max) screen-recorded video where you run the system, execute your tests, and explain your key design decisions. Submissions without a video will not be reviewed.</p>|
| :- |

|<p>**Key Details**</p><p>**Time Limit:** 7 calendar days</p><p>**Stack:** .NET 10, ASP.NET Core Web API, Wolverine, EF Core, PostgreSQL</p><p>**Must Include:** docker-compose.yml that brings up the entire system with one command</p><p>**Delivery:** GitHub repo + video link in README</p>|
| :- |

## **Evaluation Criteria**

|**Criteria**|**Weight**|**What We Look For**|
| :- | :- | :- |
|Correctness Under Load|30%|Does it actually work when 50 requests hit at once? Prove it.|
|Video Walkthrough|25%|Can you explain WHY in 10 minutes? Can you demo it working live?|
|API & Data Design|20%|Clean contracts, proper schema, sensible status machines|
|Code Quality|15%|Readable, consistent, properly layered, good error handling|
|Testing Strategy|10%|Meaningful tests that catch real problems, not just happy paths|


# **The Project: Order Processing Service**
You are building the back-end for an internal order management system used by a mid-size e-commerce company. The system handles the full lifecycle of an order: creation, payment processing, inventory reservation, fulfillment, and refunds.

This is not a simple CRUD app. The challenge is handling concurrent operations correctly, designing a robust state machine, and proving it all works under realistic conditions.

## **Domain Overview**

|**Entity**|**Description**|
| :- | :- |
|**Product**|Has a name, SKU, price, and stock quantity. Stock must be tracked accurately under concurrent orders.|
|**Order**|Contains one or more line items, each referencing a product and quantity. Has a strict status lifecycle.|
|**Payment**|Linked to an order. Simulated (no real gateway). Must support idempotent processing and refunds.|
|**Inventory Log**|Immutable audit trail of every stock change: reservation, release, deduction, restock.|


# **Architecture: Wolverine Usage**
This project must use Wolverine as the core messaging and handler framework. We are specifically evaluating your ability to leverage Wolverine’s features, not just use it as a MediatR replacement.

## **Required Wolverine Patterns**
- **Command Handlers:** All write operations (create order, confirm order, process payment, etc.) must be implemented as Wolverine command handlers, not controller-level logic.
- **HTTP Endpoint Routing:** Use Wolverine.HTTP for endpoint routing where appropriate. You may use a mix of Wolverine.HTTP endpoints and traditional ASP.NET Core controllers — explain in DECISIONS.md why you chose one vs the other for each endpoint.
- **Side Effects:** Use Wolverine’s cascading messages or side effects for operations that should happen after the primary command succeeds (e.g., logging an inventory entry after stock reservation, simulating a webhook callback after payment).
- **EF Core Integration:** Use Wolverine’s built-in EF Core transactional middleware for automatic transaction management on handlers that modify database state.

### **Optional Wolverine Features (Bonus)**
- Use Wolverine’s Saga or Durable Workflow for the order lifecycle state machine
- Use Wolverine’s built-in retry policies for the simulated payment processing
- Use Wolverine’s local queue for durable background processing (payment expiry, order timeout)

|<p>**Why Wolverine?**</p><p>We use Wolverine in production. We want to see that you can learn and apply a framework’s idioms, not just force your existing patterns onto it. Read the Wolverine docs at wolverine.netlify.app and use the framework the way it was designed to be used.</p>|
| :- |


# **Part 1: Order Lifecycle & State Machine**
Implement the complete order lifecycle. The order must move through states in a strict sequence, and invalid transitions must be rejected.

### **Order Status Flow**

|<p>DRAFT  ───>  CONFIRMED  ───>  PAYMENT\_PENDING  ───>  PAID  ───>  FULFILLING  ───>  SHIPPED  ───>  DELIVERED</p><p>`  `│            │                   │                        │</p><p>`  `│            │                   │                        │</p><p>`  `└─> CANCELLED  └─> CANCELLED        └─> PAYMENT\_FAILED        └─> REFUND\_REQUESTED ─> REFUNDED</p>|
| :- |

### **Requirements**
1. **Create Order (DRAFT):** Customer creates an order with one or more line items (product ID + quantity). No stock is reserved yet. Order can be modified freely while in DRAFT.

1. **Confirm Order (DRAFT → CONFIRMED):** Validates all products exist and requested quantities are available. Reserves stock for each line item (reduces available quantity). If any product has insufficient stock, the entire confirmation must fail atomically — no partial reservations.

1. **Process Payment (CONFIRMED → PAYMENT\_PENDING → PAID):** Initiates a simulated payment. The payment must be idempotent: the client sends an idempotency key, and retrying with the same key must not charge twice. Simulate a 2-second processing delay (Task.Delay) to mimic a real gateway. Randomly fail 20% of payments (to test the PAYMENT\_FAILED path).

1. **Cancel Order (DRAFT or CONFIRMED → CANCELLED):** If the order was CONFIRMED, all reserved stock must be released back. Cancellation must be atomic — if stock release fails, the order must not be marked as cancelled.

1. **Fulfill & Ship (PAID → FULFILLING → SHIPPED → DELIVERED):** PAID → FULFILLING: converts reserved stock into committed stock (the items are being packed). FULFILLING → SHIPPED: records a tracking number. SHIPPED → DELIVERED: marks the order as complete.

1. **Refund (DELIVERED → REFUND\_REQUESTED → REFUNDED):** Creates a refund record linked to the original payment. Restocks the items. Refund amount must equal the original payment amount.

|<p>**The Hard Part**</p><p>The state machine must enforce that ONLY valid transitions are allowed. For example: you cannot go from DRAFT directly to PAID, you cannot cancel a SHIPPED order, and you cannot confirm an already confirmed order. Every invalid transition must return a clear error message stating the current status and which transitions are allowed from it.</p>|
| :- |


# **Part 2: Concurrent Stock Management**
This is the core engineering challenge. Stock management under concurrency is where most systems break, and it is the primary thing we are evaluating.

## **The Problem**
Imagine Product A has 5 units in stock. Three customers simultaneously try to order 3 units each. Only one should succeed (leaving 2 units), or at most one gets 3 and one gets 2 — but the system must never allow 9 units to be reserved from 5 available.

## **Requirements**
1. Stock reservation during order confirmation must be concurrency-safe. Two simultaneous confirmations for the same product must not oversell.
1. You must choose and implement a specific concurrency strategy: optimistic concurrency (EF Core concurrency tokens), pessimistic locking (SELECT FOR UPDATE), or serializable transactions. Document which one you chose and why.
1. Every stock change must be logged in the Inventory Log as an immutable entry: reservation, release (on cancel), deduction (on fulfill), and restock (on refund).
1. The current stock level must always equal: initial stock + sum(restocks) – sum(deductions). This invariant must hold even under concurrent operations.

|<p>**How to Prove It Works (Required)**</p><p>Write a test (integration test or standalone console app) that does the following:</p><p>1\. Seed a product with exactly 10 units of stock</p><p>2\. Fire 20 concurrent order confirmation requests, each trying to reserve 1 unit</p><p>3\. Wait for all requests to complete</p><p>4\. Assert: exactly 10 orders are CONFIRMED, exactly 10 are rejected, stock is exactly 0</p><p>5\. Print the results to console</p><p>**Include the console output of this test as a screenshot or text file in your repo (tests/concurrency-proof.txt). Run this test live in your video walkthrough.**</p>|
| :- |


# **Part 3: Idempotent Payment Processing**
Payment processing must be idempotent. This means the client can safely retry a payment request and the system will not process it twice.

## **Requirements**
1. The client sends a POST /api/orders/{id}/pay with an Idempotency-Key header.
1. If this is the first time seeing this key: process the payment (with simulated 2s delay and 20% random failure).
1. If this key was already used: return the same response as the original request (same status code, same body) without re-processing.
1. Idempotency keys must be scoped to the order. The same key on a different order is treated as a new request.
1. Store idempotency records with: key, order ID, response status code, response body, and created timestamp.
1. Idempotency records should expire after 24 hours (a background cleanup job or TTL-based approach).

### **Edge Cases You Must Handle**
- What if two requests with the same idempotency key arrive at the exact same time? Only one should process.
- What if the first request failed (PAYMENT\_FAILED)? A retry with the same key should return the failure, not retry the payment.
- What if a request with the same key but different order ID is sent? Reject it with a clear error.

|<p>**How to Prove It Works (Required)**</p><p>Write a test that:</p><p>1\. Creates and confirms an order</p><p>2\. Sends 10 concurrent payment requests with the SAME idempotency key</p><p>3\. Asserts that only 1 payment record exists in the database</p><p>4\. Asserts all 10 responses are identical</p><p>**Save the output to tests/idempotency-proof.txt. Run this test live in your video.**</p>|
| :- |


# **Part 4: Design Decisions Document**
Create a DECISIONS.md file in your repository. For each topic below, explain what you chose, what alternatives you considered, and why your choice is better for this specific system. There are no right answers — we want to see your reasoning.

1. **Concurrency Strategy:** Which locking mechanism did you use for stock reservation? What are the trade-offs vs the alternatives? Under what conditions would your choice break down?

1. **Idempotency Storage:** Where do you store idempotency records? Same database? Redis? In-memory? What are the failure modes of your choice? What happens if the idempotency store goes down?

1. **State Machine Implementation:** How did you implement the order state machine? Hard-coded switch/case? State pattern? Enum with valid transitions dictionary? Why? Would your approach scale if we added 5 more statuses?

1. **Error Handling Strategy:** How do you translate domain errors into HTTP responses? Do you use middleware, exception filters, or result types? How do you ensure a caller gets a useful error message without leaking internal details?

1. **Database Transaction Boundaries:** Where do your transactions begin and end? What is the largest transaction scope in your system? Why did you draw the boundary there? What would happen if you made it smaller or larger?

1. **Wolverine vs Traditional ASP.NET:** Where did Wolverine’s patterns help you and where did they feel like overhead? Which endpoints did you route through Wolverine.HTTP vs standard controllers, and why? If you used cascading messages, explain a specific case where they simplified your code.


# **Part 5: Video Walkthrough (10 Minutes Max)**
Record a screen recording of up to 10 minutes. Share your screen with your IDE and terminal visible. Run the system, execute your tests, and explain your reasoning. Keep it tight and focused.

## **Cover These In Order**

1. **Run the system (2 min):** docker-compose up, show it starts clean. Hit a few endpoints to demonstrate the order lifecycle end-to-end (create → confirm → pay → ship → deliver). Show one invalid transition and the error response.

1. **Concurrency + idempotency proof (3 min):** Run both proof tests live. Show the console output. Briefly explain the concurrency strategy you chose and why.

1. **Explain your hardest decision (3 min):** Pick the one design decision from DECISIONS.md that you struggled with the most. Tell us what you tried first, why it didn’t work, and what you landed on.

1. **One thing you’d change (2 min):** If you had another week, what would you refactor or do differently? Be specific.

|<p>**Video Guidelines**</p><p>Use Loom (free), OBS, or any screen recorder. Audio narration is required. Upload to Loom, YouTube (unlisted), or Google Drive and include the link in your README.</p><p>**10 minutes maximum. Submissions without a video will not be reviewed.**</p>|
| :- |


# **Bonus Challenges (Optional)**
Only attempt after Parts 1–5 are solid.

- **Background order expiry:** Orders in PAYMENT\_PENDING for more than 10 minutes should be automatically cancelled (releasing stock). Implement as a BackgroundService.

- **Structured logging with Serilog:** Add correlation IDs that trace a request from controller through all service calls to database operations.

- **API rate limiting:** Add rate limiting per customer using ASP.NET Core’s built-in rate limiter. Show it working in your video.

- **Read-model optimization:** Add a denormalized “order summary” table that is updated via domain events or EF interceptors, optimized for listing/searching orders without joining multiple tables.


# **Submission Checklist**

|☐|GitHub repository with clean commit history (meaningful commits, not one giant commit)|
| :- | :- |
|☐|docker-compose up starts the full system (app + PostgreSQL) and applies migrations|
|☐|DECISIONS.md with all 6 design decision explanations|
|☐|Concurrency proof: test + saved output in tests/concurrency-proof.txt|
|☐|Idempotency proof: test + saved output in tests/idempotency-proof.txt|
|☐|Unit tests for state machine transitions (all valid + all invalid transitions)|
|☐|Integration tests for critical API flows|
|☐|README with build instructions, API documentation, and video link|
|☐|10-minute (max) video walkthrough link in README|

## **What We Value**

- A system that actually works under concurrency — not one that only works in Postman one request at a time
- Evidence over claims: show us the test output, don’t just tell us “it handles concurrency”
- Clear thinking about trade-offs: every design decision has downsides, and we want to hear them
- Honest communication: if you ran out of time, say so. If you took a shortcut, explain why.
- Code that a new team member could read and understand without a 30-minute explanation


|<p>**Final Note**</p><p>We do not expect perfection. We expect a working system, honest documentation of your decisions, and a 10-minute video that shows us how you think as an engineer.</p><p>**Good luck. We look forward to your submission.**</p>|
| :- |

