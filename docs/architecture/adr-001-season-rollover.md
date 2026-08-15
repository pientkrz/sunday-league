# ADR-001: Clone season structure during rollover

## Status

Accepted

## Context

Final standings must apply promotion and relegation without changing historic fixtures, league membership, or published tables.

## Decision

An Owner rolls an active, completed season into an empty later draft season. The operation clones leagues and teams, reassigns promoted and relegated teams from confirmed standings, copies scoped board memberships, and leaves playoff places for manual administration. The old season remains unchanged until the Owner archives it and activates the prepared draft.

## Rationale

Cloning preserves immutable history and permits the next season to reuse stable public league slugs. The operation is restricted to an empty draft season and runs transactionally to avoid partial rollovers.

## Trade-offs

The Owner must explicitly archive and activate seasons after preparation. Playoffs are not inferred automatically because their rules vary between leagues.

## Consequences

Historical results remain accurate, scoped administrators receive access to the corresponding new league, and future work can add a playoff-resolution step without changing the archived data model.
