# DECISIONS

## 1) Concurrency Strategy
I used optimistic concurrency on `Product` via PostgreSQL row versioning (`xmin` mapped by EF row version), plus retry on `DbUpdateConcurrencyException` in the `ConfirmOrder` handler. This keeps high throughput without holding long DB locks. Trade-off: under extreme contention, retries increase latency and some operations fail after max retries.

## 2) Idempotency Storage
I store idempotency records in the same PostgreSQL database (`IdempotencyRecords` table) with a unique index on `Key`. This gives strong consistency with order/payment updates and avoids split-brain behavior between two data stores. Failure mode: if DB is down, idempotency checks and payment operations are unavailable.

## 3) State Machine Implementation
I implemented the state machine as an enum + allowed transition dictionary in the `Order` aggregate. This centralizes transition rules and enables clear invalid-transition errors showing allowed next statuses. It scales better than scattered `switch` checks because new statuses only require dictionary updates and targeted handler changes.

## 4) Error Handling Strategy
Domain validation failures throw `DomainValidationException`, translated by endpoints to `400 Bad Request` with explicit messages. This keeps domain logic independent of HTTP while still returning actionable client errors.

## 5) Transaction Boundaries
Transaction boundary is a single command handler invocation (through Wolverine + EF Core). For multi-entity writes (confirm, cancel, refund), all updates are committed atomically at handler completion. Smaller boundaries risk partial writes; larger boundaries increase lock duration and contention.

## 6) Wolverine vs Traditional ASP.NET
Write operations are implemented as Wolverine command handlers. HTTP routing is done with ASP.NET Minimal APIs that dispatch to Wolverine (`IMessageBus`) so endpoint bodies remain thin and orchestration stays in handlers. This balances explicit HTTP control with Wolverine-centric command execution and transactional middleware.

## 7) Omission of the Repository Pattern
   Initially, I considered implementing the Repository Pattern (IOrderRepository, IProductRepository) to abstract the database. However, I intentionally removed it. In a CQRS architecture utilizing Wolverine, injecting EF Core's AppDbContext directly into the Command Handlers is the preferred, idiomatic approach. AppDbContext natively implements the Unit of Work pattern, and DbSet<T> serves as the repository. Introducing a custom repository layer over EF Core would have been a redundant abstraction, creating dead code and complicating Wolverine's automatic transactional middleware.

## 8) PCI Compliance & Payment Gateway Mocking
   In InitiatePaymentCommand, the API accepts raw credit card data (PAN, CVC, Expiry) to perform basic domain validation (Luhn algorithm, expiry checks). I acknowledge this is a severe PCI DSS compliance violation in a production environment. In a real-world scenario, the backend API should never touch raw card data. Instead, I would implement a Tokenization pattern (e.g., Stripe or Braintree) where the frontend exchanges raw card data for a secure, single-use token directly with the gateway, and the API only processes that token. I chose to accept raw card data here strictly to fulfill the assessment's requirement of demonstrating rich domain validation and simulating a gateway.
   
## 9) The challenges I face with working with wolverine and how I overcome them
I fully utilized Wolverine's transactional middleware to eliminate SaveChangesAsync() from my core command handlers, ensuring my database updates and Outbox events commit atomically. The only places I explicitly call SaveChanges are inside my Idempotency handlers to catch Unique Constraint violations in-flight, and inside my terminal background event consumers to safely commit side effects.
(I ALOSO DISCOVERED NAMING MY HANDLERS CLASS WITH AN "S" AT THE END MAKES THEM NOT WORK LOL)