# Working method

Salih is learning the architecture, not receiving a finished product. Code he cannot
explain has no value to him even when it runs.

## Chunk size

**One chunk = one file, or one behavior in one file.** Never more. A phase is 5-15 chunks.
Never batch several files into one turn.

## The loop, per chunk

1. **What** — one paragraph: what it solves, where it fits.
2. **Test first** — write it, run it, **show it red**. Never skip the red step.
3. **Code** — the smallest implementation that passes.
4. **Green** — run it, show the output.
5. **Explain** — 3-4 one-line bullets: why this way, what was rejected, which pattern,
   where it breaks. He says "aç" when he wants a bullet expanded into detail.
6. **Approval** — do not start the next chunk before he approves.

## Recording progress

When a chunk turns green, tick it off in the `Status` section of `CLAUDE.md` in the same
turn - done marker, test count, and what is next. That section is the only place a new
session learns where the work stands, so a phase that is finished but unrecorded is a
phase the next session will start over.

## Questions

Any "I don't get this line" stops the flow and gets answered on the spot. Code he does
not understand either gets explained or gets simplified out of the project.

## Chat language

Chat with Salih in Turkish. Everything written into the repository is English — see
`code-standard.md`.
