# quicksheet-todo

Task management extension for [QuickSheet](https://github.com/cemheren/QuickSheet) — manage your to-do list right inside your spreadsheet cells.

## Quick Start (3 keystrokes per action)

```
todo: Buy groceries       ← just type it, no "add" needed
todo: 1                   ← toggles task #1 done/undone
todo: -1                  ← removes task #1
todo:                     ← shows your list
```

That's it. Most workflows need just one cell edit.

## Features

- **Add tasks** with optional priority and due dates
- **4 priority levels**: `!low`, `!normal`, `!high`, `!critical`
- **Due date tracking** with overdue warnings (⚠️)
- **One-keystroke toggle** — type the task number to mark done/undone
- **Quick remove** — prefix number with `-` to delete
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
| `todo:` or `todo: list` | Show all tasks sorted by priority/status |
| `todo: Buy groceries` | Add a task (just type it — no keyword needed) |
| `todo: 3` | Toggle task #3 done ↔ undone |
| `todo: -3` | Delete task #3 |
| `todo: add !high Fix login bug` | Add with high priority |
| `todo: add @2026-05-20 Ship v1.0` | Add with due date |
| `todo: done 3` | Explicitly mark task #3 complete |
| `todo: undo 3` | Explicitly reopen task #3 |
| `todo: rm 3` | Delete task #3 (same as `-3`) |
| `todo: clear done` | Remove all completed tasks |
| `todo: stats` | Show summary with progress bar |

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
