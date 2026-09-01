# GitHub e atualização automática — Bebel 155 v5.1.7

Repositório atual:

`https://github.com/Bebel-155/bebel157`

O aplicativo consulta a API pública:

`https://api.github.com/repos/Bebel-155/bebel157/releases/latest`

## Nomes curtos dos artefatos

- EXE: `Bebel-155_V5_1_7.exe`
- Setup: `Bebel-155_Setup_V5_1_7.exe`
- Pacote desta versão: `Bebel-155_V5_1_7.zip`

Releases futuras seguem o mesmo formato, por exemplo:

- `Bebel-155_V5_1_8.exe`
- `Bebel-155_Setup_V5_1_8.exe`

## Compatibilidade

O atualizador aceita tanto o novo nome `Bebel-155_V*.exe` quanto os nomes
longos antigos `Bebel_Equipe_Do_Mais_Novo_155*.exe`.

## Migração automática

Se `%LOCALAPPDATA%\BebelEquipe155\settings.ini` ainda tiver:

`github_repo=Bebel-155/bebel155`

a v5.1.7 troca automaticamente para:

`github_repo=Bebel-155/bebel157`

## Publicação

Execute `PUBLICAR_NO_GITHUB.bat`.

O workflow `.github/workflows/release.yml` cria EXE, Setup, hashes SHA-256
e uma Release marcada como `latest` em cada push para `main`.
