# SpawnConfig
Allows you to control the spawns of entities on your server.

## Features

- **Spawn Control**: Define spawn chances for entities to control their frequency.
- **Configurable**: Easily customize the configuration to fit your server's needs.
- **Automatic Cleanup**: Remove entities with a spawn chance of 0.

## Configuration

```json
{
  "Enabled": false,
  "Spawns": []
}
```
- Enabled: if set to true it enables the plugin.
- Spawns: list of entities's prefabs and it's spawn chances (auto generated when you first load the plugin).
