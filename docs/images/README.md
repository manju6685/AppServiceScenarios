# Screenshot capture guide

The main [README](../../README.md) currently uses **hotlinked Microsoft Learn screenshots** (© Microsoft, CC-BY 4.0) for inline images, which means no PNG files are required for the README to render correctly on GitHub.

This folder is reserved for **future overrides** — if you want to replace the Microsoft Learn screenshots with your own (for example to match a different Visual Studio theme, a different region of the Azure Portal, or a specific extension version), capture them as described below and update the corresponding `<img src="...">` URL in `../../README.md` to point at `docs/images/<filename>.png`.

> Tip: keep each PNG under **300 KB**. Crop tightly to the relevant dialog
> — do not capture the whole desktop.

## Suggested filenames

| Filename                             | What to capture                                                                                  | Currently sourced from                                       |
| ------------------------------------ | ------------------------------------------------------------------------------------------------ | ------------------------------------------------------------ |
| `vs-publish-rightclick.png`          | Visual Studio 2022 Solution Explorer with the right-click context menu open, **Publish…** highlighted | Microsoft Learn — `right-click-publish.png`                  |
| `vs-publish-azure-target.png`        | The first page of the Publish wizard with **Azure** selected                                     | Microsoft Learn — `vs-publish-target-azure.png`              |
| `vs-publish-select-appservice.png`   | The **App Service** picker page showing the subscription / resource-group tree and the **+ Create new** link at the top-right | Microsoft Learn — `publish-new-app-service.png`              |
| `vs-publish-create-appservice.png`   | The **Create App Service** dialog with Name / Resource Group / Hosting Plan fields filled in     | Microsoft Learn — `web-app-name.png`                         |
| `vscode-azure-tree.png`              | VS Code with the Azure extension open in the side bar, **App Services** node expanded            | _Not hotlinked — link out to the Learn walkthrough instead_  |
| `portal-diagnose-and-solve.png`      | Azure Portal showing the **Diagnose and solve problems** blade for an App Service                | Microsoft Learn — `app-service-diagnostics-homepage.png`     |

## How to capture cleanly on Windows

1. Press `Win + Shift + S` to open the snipping toolbar.
2. Choose **Rectangle snip** and drag tightly around the dialog or pane.
3. Open **Paint** (or any image editor), paste, and save as PNG into this folder
   using the exact filename from the table above.

## How to capture cleanly on macOS

1. Press `Cmd + Shift + 4`, then drag to select the area.
2. The PNG is saved to your Desktop; rename it to match the filename above
   and move it into this folder.
