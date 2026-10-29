# Home DevOps

Azure DevOps pessoal e local, em escala bem menor: projetos, features e bugs com tarefas, para saber o que já foi desenvolvido em cada projeto. WPF + .NET 10.

Cada projeto é um arquivo `.hdp` (JSON) em `Documentos\HomeDevops Projects`. As configurações ficam em `~\.homeDevops\config.json`.

## Estrutura

| Pasta | O que é |
|---|---|
| `src/HomeDevops.Core` | Modelos e regras (estados, nomes de arquivo, armazenamento `.hdp`, backup, configuração, tema). Sem WPF. |
| `src/HomeDevops.App` | Aplicativo WPF (MVVM com CommunityToolkit.Mvvm): janelas, estilos, view models. |
| `tests/HomeDevops.Tests` | Testes de unidade do Core (xUnit). |

## Comandos

```powershell
dotnet run --project src/HomeDevops.App   # abre o app
dotnet test                                # roda os testes
```

Para rodar sem mexer nos seus dados reais, aponte as pastas para outro lugar antes de abrir:

```powershell
$env:HOMEDEVOPS_PROJECTS_DIR = "C:\temp\hd\projetos"
$env:HOMEDEVOPS_CONFIG_DIR   = "C:\temp\hd\config"
```
