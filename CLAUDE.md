# Legion Agent Harness

**Note Well:**

We're currently working on the 'WebDev' project.  You can ignore 'WebHost' for now - it'll come into play later.  

## Projects
- src/aspire/Legion.AppHost - the Aspire Orchestrator
- src/aspire/Legion.ServiceDefaults
- src/Legion.Agents - the main library for building AI Agents
- src/Legion.WebHost - the main application **when using aspire to host the application**
- src/libs/Legion.Admin.Auth - authentication library
- src/libs/Legion.Admin.Data - EF Core Contexts and Models
- src/libs/Bridage.Admin.UI - razor pages that will be consumed by multiple projects
- src/orleans/Legion.SiloHost.Abstractions - TBD
- src/orleans/Legion.SiloHost.Client - TBD
- src/orleans/Legion.SiloHost.Common - TBD
- src/orleans/Legion.SiloHost.Grains - TBD
- src/orleans/Legion.SiloHost.Server - TBD
- src/WebDev - All UI development is being done in this project.  