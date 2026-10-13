“Why is ChatGPT/Codex suddenly using so much RAM? I’m not even doing anything right now.”

That was my starting point. When I investigated, one provider’s official MCP integration alone accounted for over 14 GiB of committed memory across its running instances. I wasn’t actively using that integration. The measurement includes memory that can be paged out; it isn’t a physical-RAM benchmark.

So I built a tool to see what was happening and take control—with ChatGPT/Codex itself.

The result is Resource Desk: a small, portable Windows companion that shows system RAM, memory attributed to Codex, and process counts per integration. You can inspect the breakdown, disable supported local MCP integrations, stop their verified processes, and enable them again when needed.

One detail matters: “enabled = false” does not mean existing processes have stopped. Configuration and running processes need separate visibility.

The idea is in the same spirit as Claude Mods: making your AI tools fit the way you work. Resource Desk takes a simpler, external approach, working with local processes and configuration rather than extending the app from within. I’d love to have this kind of resource control directly in ChatGPT/Codex. For now, this is my first practical way to help myself—and perhaps others.

It’s now open source: native Windows UI, portable ZIP, no telemetry. An early preview for local MCP servers in the global Codex configuration. Shared helpers stay protected; existing sessions may need a Codex restart to apply changes.

Have you noticed integrations consuming resources while you weren’t using them?

https://github.com/Kiragroh/codex-resource-desk

Image 1: cover artwork created with GPT Image. Image 2: an unedited screenshot of the running app, showing a snapshot of my own machine.

Independent project. Not affiliated with OpenAI.

#Codex #MCP #OpenSource
