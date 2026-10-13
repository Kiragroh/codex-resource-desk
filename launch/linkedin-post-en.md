Why are so many Node processes running when I’m not even using those tools?

That question led me to build Resource Desk with Codex: a small, portable Windows app for understanding and managing the resources used by Codex and its local MCP integrations.

It shows system memory, private RAM attributed to Codex, and process counts per integration. Click the memory breakdown to inspect the details. You can disable supported local MCP integrations and selectively stop their verified processes. Disabled entries stay visible so you can enable them again later.

One useful lesson from building it: “enabled = false” does not mean existing processes have stopped. Resource Desk shows configuration and running processes separately for exactly that reason.

The first version is now open source on GitHub: native Windows UI, portable ZIP, no telemetry. It’s an early preview focused on local MCP servers in the global Codex configuration. Shared Codex helpers are protected, and existing sessions may need a Codex restart to pick up new settings.

If you use Codex on Windows, which integration would you inspect first?

https://github.com/Kiragroh/codex-resource-desk

Image 1: cover artwork created with GPT Image. Image 2: an unedited screenshot of the running app, showing a snapshot of my own machine.

Independent project. Not affiliated with OpenAI.

#Codex #MCP #OpenSource
