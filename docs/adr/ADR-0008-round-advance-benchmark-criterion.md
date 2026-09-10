# ADR-0008 — Round-advance benchmark criterion

- Status: Accepted
- Date: 2026-09-10

## Context

`docs/context/02-revisao-arquitetura.md` §5 requires the benchmark number to be
fixed *before* measuring, because a threshold chosen after seeing the result
only ratifies whatever the engine already does. The number was never set, which
left `PERF-001` undefined and `MATCH-011` (`FastMatchSimulation`) blocked on
nothing in particular.

Two facts had to be corrected before the number could be chosen honestly.

**The calibration in the source documents is stale.** Both
`02-revisao-arquitetura.md` §5 and `00-roadmap-guia.md` Trilha B calibrate
against "90 minutes at 100 ms = 54,000 ticks per match". `MATCH-006` revised the
timestep to 50 ms, and `MatchSimulation` derives `_totalTicks` from
`MatchTuning.MatchMinutes * 60 / secondsPerTick`. A match is **108,000 ticks**.
Anchoring on the written figure would anchor on half the real work. This ADR
outranks those documents, which are never edited in place.

**The "about 60 ms per match" figure is not produced by anything in the
repository.** There is no timing instrumentation: the only occurrence of
`Stopwatch` anywhere is the banned-API string inside `DeterminismGuardTests`.
The number is an ad-hoc observation from a past session, not a reproducible
measurement. A criterion measured by nothing is unfalsifiable, so the criterion
below is derived from the player's experience rather than from that figure.

## Decision

**Advancing one round — 100 matches simulated in detail — must cost at most 10
seconds on a single thread on the reference machine.**

**Reference machine.** The owner's development machine, not a CI runner. Shared
runners vary with neighbours and with silent hardware generation changes, so a
number measured there cannot be re-measured in two years. `PERF-001` records the
machine's CPU model, physical core count, OS and .NET runtime version in this
ADR when the harness first runs.

**Single thread is deliberately the pessimistic bound, not a performance
target.** Match isolation is already implemented and asserted by
`MatchIsolationTests`: a match owns its state and its `IRandomSource` and cannot
mutate the career it came from, so a round parallelises across cores and stays
deterministic. That parallelism is headroom the architecture already paid for.
It must not be counted toward the criterion, and the criterion must not be
tightened on the assumption that it exists.

**Remedies are ordered, and a second engine is last.** When the budget is
exceeded:

1. Reduce the number of competitions simulated in detail. `02-revisao-arquitetura.md`
   §5 names this as the variable under control — not the engine.
2. Optimise the existing simulation.
3. Only then consider a second simulator.

Without this ladder, "the benchmark failed" collapses into "therefore
`FastMatchSimulation`", and a criterion written to *avoid* a second set of
football rules becomes the trigger for one. Introducing `FastMatchSimulation`
requires an ADR that supersedes this one and states which of the first two
remedies were tried and why they were insufficient.

**Measurement must be reproducible.** `PERF-001` builds a minimal harness: run N
matches, report milliseconds per match, print CPU, core count and runtime
version. Not a benchmarking framework.

**CI guards the order of magnitude, never the absolute number.** Asserting 10
seconds on a shared runner produces red builds from noise, and a test that fails
randomly is one the team learns to ignore — worse than no test. CI catches a 10x
regression and tolerates 30% drift.

## Consequences

### Positive

- The go/no-go on a second football engine has a stated threshold instead of a
  vague sense that things feel slow.
- The budget is generous where being wrong is cheap. Too loose is fixed by
  simulating fewer competitions in detail — a settings change. Too tight invokes
  a second rules engine, which is the architectural failure the project is built
  to avoid, and is irreversible in practice.
- Nothing in the criterion depends on the unverified 60 ms figure, so it stays
  meaningful whatever the first real measurement says.
- With deterministic parallelism, a 10-second single-thread budget is roughly
  two seconds of wall clock on a four-core machine.

### Negative

- A 10-second single-thread round advance is uncomfortable if `CAREER-00` ever
  advances a round synchronously with no progress feedback. That is a UI problem
  to solve in `CAREER-00` with streamed results, not a reason to tighten this
  number against a UI that does not exist.
- The criterion assumes 100 detailed matches per round. `COMP-00` does not exist
  yet, so that figure is a design intent rather than a measured workload, and it
  is the part of this ADR most likely to need revisiting.
- Guarding only the order of magnitude in CI means a gradual 3x regression can
  land unnoticed between manual runs on the reference machine.

## Follow-up

`PERF-001` implements the harness, records the reference machine's
specification here, and produces the first real measurement. Until it runs, this
ADR states a criterion that nothing has yet been measured against, and no
performance claim in the repository may be presented as verified.
