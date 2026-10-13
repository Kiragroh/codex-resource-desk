# LinkedIn launch kit

The post is a draft; it has not been published to LinkedIn.

Upload the images in this order, then paste `linkedin-post-en.md`. The earlier German draft is retained in `linkedin-post-de.md` for reference.

1. **01-gptimage-cover.png** — GPT Image title graphic. Alt text: “Resource Desk: Know what Codex is running. Blue and purple bars and a control switch on a dark background. A portable, open-source Windows tool for RAM, process counts and local MCP controls. Early preview; independent of OpenAI.”
2. **02-app-screenshot.png** — unchanged native application capture, 13 October 2026, 15:12 Europe/Berlin. Alt text: “Resource Desk shows 23.1 GiB used of 63.7 GiB system RAM, 4.4 GiB private Codex RAM and 217 attributed processes. Hostinger and Unity are disabled with zero local processes. The sidebar explains that configuration and runtime are independent.”

The screenshot was visually reviewed: no usernames, credentials, config contents, filesystem paths, patient data or document titles are visible. Its values describe one workstation at one instant. The cover is an illustration, not a benchmark or a fabricated application screenshot. No image retouching was applied to the application capture.

The original product logo and this cover were made with the built-in GPT Image tool. The logo reference is `src/ResourceDesk.App/Assets/logo.png`.

## Editorial checks for the revised post

- The local audit recorded 14.67 GiB of **private committed memory** across 239 Hostinger-related Node processes at 13:16:55 on 13 October 2026. The configured integration comprised nine Hostinger components with multiple running instances. This is an aggregate for one provider's integration, not one server process; committed memory is not an exclusive physical-RAM measurement. Raw local audit files and configurations are not published.
- Provider provenance: [Hostinger's own MCP documentation](https://www.hostinger.com/support/11079316-hostinger-api-mcp-server/) links its maintained server. “Official” refers to the provider, not OpenAI endorsement.
- Comparison: [Anthropic's Claude Mods documentation](https://code.claude.com/docs/en/plugins/mods/overview) describes handlers that run inside Claude Code and can change its UI and behavior. Resource Desk is an external Windows companion. The post makes no blanket claim that ChatGPT/Codex has no official extension capabilities.

## Cover generation prompt

Create a polished square 1:1 LinkedIn product announcement graphic for Resource Desk, an independent Windows app for inspecting Codex RAM and controlling local MCP integrations. Use the attached image as the exact product-logo reference; keep its three blue/purple meter bars and blue/purple toggle recognizable. This is an editorial launch graphic, NOT an app screenshot. Main headline, exactly: 'Know what Codex is running.' Product name, exactly: 'Resource Desk'. Supporting line, exactly: 'RAM · Processes · MCP controls'. Small bottom badge, exactly: 'WINDOWS  |  PORTABLE  |  OPEN SOURCE'. Small top-right badge, exactly: 'EARLY PREVIEW'. Small footer, exactly: 'Independent tool. Not affiliated with OpenAI.' Restrained, exceptionally clean premium software editorial design, dark graphite #0D1117 background, UKL blue #008AC9 with UKL purple #8064A2 accents and crisp white typography. Generous whitespace, large readable type, headline at top-left, product name just beneath, large crisp logo as central-right visual. Subtle small connected process nodes as background motif near logo only, without numbers or fake measurements. Strong grid, optical balance, comfortable margin. Do not invent a UI, charts, benchmark savings, fake dashboard panels or corporate logos. No green. No people, no hands, no device mockup. Avoid crowded layout. Make it feel like a useful open-source developer tool with clear controls, not an ad full of hype.
