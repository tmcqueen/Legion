# Brigade Agent Harness

**Note Well:**

We're currently working on the 'WebDev' project.  You can ignore 'WebHost' for now - it'll come into play later.  

## Projects
- src/aspire/Brigade.AppHost - the Aspire Orchestrator
- src/aspire/Brigade.ServiceDefaults
- src/Brigade.Agents - the main library for building AI Agents
- src/Brigade.WebHost - the main application **when using aspire to host the application**
- src/libs/Brigade.Admin.Auth - authentication library
- src/libs/Brigade.Admin.Data - EF Core Contexts and Models
- src/libs/Bridage.Admin.UI - razor pages that will be consumed by multiple projects
- src/orleans/Brigade.SiloHost.Abstractions - TBD
- src/orleans/Brigade.SiloHost.Client - TBD
- src/orleans/Brigade.SiloHost.Common - TBD
- src/orleans/Brigade.SiloHost.Grains - TBD
- src/orleans/Brigade.SiloHost.Server - TBD
- src/WebDev - All UI development is being done in this project.  