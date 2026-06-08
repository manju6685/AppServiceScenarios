# AppServiceScenarios

A tiny ASP.NET Web Forms app that intentionally misbehaves on demand — used to demo App Service diagnostics (high CPU, slow responses, crashes, 500s, health checks).

**Live demo:** <https://appservicescenarios.azurewebsites.net/>

---

## Deploy it (2 minutes, browser only)

1. **Download** the prebuilt artifact: [**deploy.zip**](https://github.com/manju6685/AppServiceScenarios/releases/download/v1.0.0/deploy.zip) (8.25 MB)
2. **Create** a Windows App Service (any size, .NET Framework 4.8) if you don't have one.
3. **Open** `https://<your-app-name>.scm.azurewebsites.net/ZipDeployUI` in your browser.
4. **Drag** `deploy.zip` onto the drop zone. Wait for *Deployment successful*.
5. **Browse** to `https://<your-app-name>.azurewebsites.net/` — you'll see a page of buttons.

> No Visual Studio, no Azure CLI, no clone required.

---

## What each button does

| Page | What it does | Use it to demo |
|---|---|---|
| `/FastResponse.aspx` | Returns immediately | A healthy baseline |
| `/Slow10.aspx` | Sleeps 10 seconds | Slow response / availability |
| `/Slow60.aspx` | Sleeps 60 seconds | Request timeouts |
| `/Http500.aspx` | Returns HTTP 500 | Server errors |
| `/NullRef.aspx` | Throws `NullReferenceException` | Unhandled exceptions |
| `/StackOverflow.aspx?go=1` | Infinite recursion → w3wp crash | Process crashes, dump capture |
| `/HealthCheck.aspx` | Lightweight healthy endpoint | App Service Health Check |

After clicking a few buttons, open the App Service in the Portal → **Diagnose and solve problems** to see how the platform surfaces what happened.

---

## Run locally

```powershell
git clone https://github.com/manju6685/AppServiceScenarios.git
cd AppServiceScenarios
# Open AppServiceScenarios.sln in Visual Studio 2022 and press F5
```

The app runs on `https://localhost:44300/`.

---

## Clean up

```powershell
az group delete -n <your-resource-group> --yes --no-wait
```

---

## License

MIT
