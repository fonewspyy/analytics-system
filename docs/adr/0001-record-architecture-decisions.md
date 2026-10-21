# ADR-0001: Record architecture decisions

* Status: accepted
* Date: 2026-10-21

## Context

The work is done over three days in separate sessions. Decisions made on Day 1
(schema owner, layering) get silently reversed on Day 3 unless they are written
where the next session reads them.

## Decision

Every decision that changes structure, a library, or a public contract gets an ADR
in `docs/adr/`, numbered, copied from `0000-template.md`. ADRs are never edited
after acceptance except for status; a change of mind is a new ADR that supersedes.

Day-to-day progress goes in `WORKING_LOG.md`, not in ADRs.

## Consequences

* Read `docs/adr/` before changing structure; the README links to it.
* Reviewers can see *why* the repo looks the way it does, not only *what* it is.
