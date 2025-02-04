# configcat flag percentage update
Update percentage rules
## Usage
```
configcat [options] flag percentage update [<rules>...]
```
## Aliases
`up`
## Options
| Option | Description |
| ------ | ----------- |
| `--flag-id`, `-i`, `--setting-id` | ID of the Feature Flag or Setting |
| `--environment-id`, `-e` | ID of the Environment where the update must be applied |
| `--verbose`, `-v`, `/v` | Print detailed execution information |
| `--non-interactive`, `-ni` | Turn off progress rendering and interactive features |
| `-h`, `/h`, `--help`, `-?`, `/?` | Show help and usage information |
## Arguments
| Argument | Description |
| ------ | ----------- |
| `<rules>` | Format: `<percentage>:<value>`, e.g. `30:true 70:false` |
## Parent Command
| Command | Description |
| ------ | ----------- |
| [configcat flag percentage](configcat-flag-percentage.md) | Manage percentage rules |
