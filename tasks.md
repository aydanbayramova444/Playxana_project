## Core Systems (MVP)

| System | Complexity | Notes |
|---|---|---|
| **Interaction** (raycast + `E` to pick up) | Low | First-person player already exists |
| **Inventory / shopping list + UI** | Low | Items defined as ScriptableObjects |
| **Shelves with limited stock** | Low | Each shelf has a stock count, each item has a prefab |
| **NPC shoppers** | Medium | NavMesh: pick a target → walk to it → take it → next target |
| **Timer, win and lose** | Low | Handled by `GameManager` |
| **Shopping list / layout generation** | Optional | Randomized so rounds play out differently |