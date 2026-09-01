# Code standard

## Commit messages

A commit message is the subject line plus a short body when the change needs one —
nothing else. **No trailers of any kind**: no attribution lines, no generated-by or
co-authored-by footers, no session links. The message describes the change; metadata
belongs nowhere in it.

## English only

**Everything inside the repository is English** — identifiers, file header blocks, XML
doc comments, in-method comments, test names, assert messages, commit messages, README,
ADRs. No exceptions. Turkish belongs in chat, never in a file.

## Documentation

- XML `/// <summary>` on **every** member — public, private, and tests included. Every
  test method, every helper, every field of a test-local data class. Short and precise.
- A header block at the top of every file: what it is, which layer it belongs to, what
  it is responsible for, and what it is explicitly **not** responsible for.
- In-method comments explain **why**, never **what**.

## Style

- `_camelCase` private fields, `PascalCase` public members, `I` prefix on interfaces.
- No magic numbers — a named `const` or a ScriptableObject field.
- Group members with regions, in this exact order and with these names only:
  `Fields` (constants and fields), `Properties`, `Public Methods`, `Private Methods`,
  and `Nested Types` when the file has one. Omit a region that would be empty.
  Regions group members, they do not excuse size — see the next rule.
- A method longer than one screen gets split.
- Interfaces only for dependencies supplied from outside; not one per class.

## Tests

**Every behaviour has tests**, MonoBehaviours included, through the Humble Object pattern.
A class that cannot be tested is a badly designed class — split it rather than skipping
the test.

**Asset values do not get tests.** A test that re-asserts a serialized constant — an
orthographic size, a render scale, a bloom intensity, a light's shadow bias — is that value
written down twice. It catches nothing, because nobody changes those by accident: they are
already in a version-controlled asset, and a deliberate change shows up in the diff. What
it does do is fail every time you make a *decision*, which teaches you to stop tuning.

The line: **a test must fail when you make a mistake, never when you make a choice.** Art
values, framing, post-process strength, project settings and pipeline asset names are
choices. `PLAN.md` is where those are recorded. Logic with branches, values other code
reads as data (`BlastColor`'s byte values, which levels store raw), and rules that are
invisible until they break (asmdef dependency direction, a shader that stops resolving)
are the things worth a test.

Auditing an asset against the plan is worth doing — once, by hand, writing the findings
into `CLAUDE.md`. Freezing that audit into a permanent suite pays a one-time cost on
every run forever.
