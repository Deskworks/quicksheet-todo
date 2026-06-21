# quicksheet-todo

Task management extension for [QuickSheet](https://github.com/cemheren/QuickSheet) — manage your to-do list right inside your spreadsheet cells.

## Features

- **Add tasks** with optional priority and due dates
- **Add several at once** — `todo: add Milk; Eggs; Pay rent` (`;`-separated)
- **4 priority levels**: `!low`, `!normal`, `!high`, `!critical`
- **Due date tracking** with overdue warnings (⚠️)
- **Reference by title, not just #** — `todo: done login` instead of `todo: done 3`
- **Quick complete** — bare `todo: done` finishes the top-priority pending task
- **Completion toggle** — mark done / reopen
- **Persistent storage** — tasks survive restarts (`~/.quicksheet/todo.csv`)
- **Progress stats** with visual progress bar

## Install

In any QuickSheet cell:

```
ext: github:Deskworks/quicksheet-todo
```

## Usage

| Cell content | What it does |
|---|---|
| `todo: list` | Show all tasks sorted by priority/status |
| `todo: add Buy groceries` | Add a task (normal priority) |
| `todo: add !high Fix login bug` | Add with high priority |
| `todo: add @2026-05-20 Ship v1.0` | Add with due date |
| `todo: add !critical @2026-05-15 Deploy` | Priority + due date |
| `todo: add Milk; Eggs; !high Pay rent` | Add several tasks from one cell |
| `todo: done 3` | Mark task #3 complete |
| `todo: done login` | Complete by title match — no need to look up the # |
| `todo: done` | Complete the top-priority pending task |
| `todo: undo 3` | Reopen task #3 |
| `todo: undo` | Reopen the most recently completed task |
| `todo: rm 3` / `todo: rm login` | Delete by # or title match |
| `todo: clear done` | Remove all completed tasks |
| `todo: stats` | Show summary with progress bar |

> **Less typing, fewer lookups.** You rarely need a task number: complete or
> remove tasks by a word from their title, add a whole list in one cell, and use
> bare `todo: done` to knock out the next most important thing.

## Example output

```
📋 Todo List
# | Task                | Priority    | Due   | Status
──┼─────────────────────┼─────────────┼───────┼────────
○ 4 | Deploy to prod    | 🔴 critical | 05-15 | pending
○ 2 | Fix login bug     | 🟠 high     |       | pending
○ 1 | Buy groceries     | 🟢 normal   |       | pending
✓ 3 | Write README      | 🟢 normal   |       | done
──
3 pending · 1 done
```

## Requirements

- .NET 9 SDK
- QuickSheet with extension support

## License

MIT
