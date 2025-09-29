TripPlanner.Core.Domain

Purpose
- Holds the domain model: aggregates, value objects, and strongly-typed identifiers (IDs).
- Encapsulates business rules and invariants for trips, date options, and destination proposals.

Key building blocks
- Aggregates
  - Trip: aggregate root coordinating participants, date options, and destination proposals.
  - DateOption: proposed calendar date with simple upvote tracking.
  - DestinationProposal: proposed destination with images and votes.
- Primitives (strongly-typed IDs)
  - TripId, UserId, DestinationId, DateOptionId: wrapper record structs around Guid with New() factory and canonical ToString("D").

Design notes
- Immutability where possible: IDs and most value-like members are get-only; collections are exposed as IReadOnlyCollection and mutated internally through methods that enforce invariants.
- Minimal validation at the domain layer; application/validation layer can add richer rules (e.g., name length, content checks). The domain still guards core invariants (e.g., date range ordering, unique votes).
- Rehydration: Trip.Rehydrate is provided to rebuild aggregate state from persistence without exposing setters.

Common operations
- Trip.Create(name, organizerId): creates a new trip with cleaned name.
- Trip.AddParticipant(userId): idempotent add to participant set.
- Trip.SetDateRange(start, end): enforces end >= start.
- Trip.VoteOnDate(date, voter): creates option if missing and records voter; enforces date range if defined.
- Trip.ProposeDate(date): ensures an option exists without casting a vote.
- Trip.CastVote(dateOptionId, voter): adds vote to an existing option.
- Trip.ProposeDestination(title, description, imageUrls): creates a new destination proposal and returns its ID.
- Trip.VoteDestination(destinationId, voter): adds a unique vote for a proposal.

Conventions
- XML documentation: Public surface (classes, properties, public/internal methods) includes concise XML docs. IDs have summaries and ToString docs.
- IDs: Use IdType.New() to create; serialize using ToString() ("D" format). Treat IDs as value objects and do not parse GUIDs in UI directly.
- Collections: Exposed as read-only; mutate via intent-revealing methods only.

Testing tips
- Prefer building aggregates through Trip.Rehydrate in tests to simulate persisted state.
- Use deterministic GUIDs in tests when verifying IDs; otherwise assert behavior rather than exact GUID values.
- Assert invariants (e.g., cannot set a date range with end < start; cannot vote outside the range when set; duplicate votes are ignored).

Folder layout
- Domain/Aggregates: Aggregate roots and entities/value objects that are part of the aggregates.
- Domain/Primitives: Strongly-typed ID record structs.

Versioning & compatibility
- These domain types are consumed by multiple layers. Avoid breaking public surface area without coordinating changes across Application, Contracts, and Persistence adapters.
