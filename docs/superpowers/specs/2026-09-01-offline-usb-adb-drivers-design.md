# Bebel 155 v5.2.0 — Drivers USB/ADB Offline Integrados

Data: 2026-09-01
Status: design aprovado em conversa; aguardando revisão do documento antes do plano de implementação.

## 1. Objetivo

Adicionar ao Bebel 155 um subsistema de drivers USB/ADB offline capaz de:

- detectar o fabricante e o estado do dispositivo conectado;
- diferenciar driver ausente, driver incorreto, ADB não autorizado, ADB offline e dispositivo funcionando;
- recomendar o pacote de driver apropriado;
- instalar/reparar drivers como administrador;
- funcionar sem internet;
- oferecer uma instalação completa dos drivers incluídos;
- manter rastreabilidade de versão, origem, assinatura e SHA-256.

## 2. Princípio de distribuição

O Setup completo do Bebel 155 poderá incluir instaladores de drivers dentro de:

```text
{app}\Drivers\
```

Porém um pacote de terceiro só pode ser embarcado quando:

1. a origem oficial for identificada;
2. o arquivo for assinado digitalmente por uma entidade esperada;
3. o SHA-256 for registrado;
4. a redistribuição for permitida pela licença/termos do fabricante;
5. o pacote for compatível com as versões de Windows suportadas.

Se a redistribuição de determinado instalador não for permitida, o Bebel não inclui uma cópia não autorizada. Para essa marca, o sistema usa o melhor driver genérico redistribuível compatível ou informa que o pacote oficial não pode ser embarcado.

## 3. Fabricantes alvo

Cobertura inicial:

- Samsung
- Motorola / Lenovo
- Xiaomi / Redmi / POCO
- OnePlus
- OPPO
- Realme
- Vivo
- Huawei / Honor
- Sony
- ASUS
- Nokia / HMD
- Nothing
- ZTE
- TCL / Alcatel
- LG legado
- Google USB Driver / Android WinUSB
- Microsoft WinUSB fallback

A lista é extensível por manifesto; novos fabricantes não exigem alteração no motor de detecção se forem representáveis pelas regras existentes.

## 4. Estrutura de arquivos

```text
Drivers\
  drivers-manifest.json
  Samsung\
  Motorola\
  Xiaomi\
  OnePlus\
  OPPO\
  Realme\
  Vivo\
  Huawei\
  Honor\
  Sony\
  ASUS\
  HMD\
  Nothing\
  ZTE\
  TCL\
  LG\
  Google\
  Generic\
```

Cada subpasta poderá conter:

```text
installer.exe
driver.inf
driver.cat
driver.sys
LICENSE.txt
SOURCE.txt
```

A estrutura real depende do pacote oficial.

## 5. Manifesto de drivers

Arquivo:

```text
Drivers\drivers-manifest.json
```

Contrato lógico por pacote:

```text
DriverPackage
- Id
- Manufacturer
- DisplayName
- Version
- PackageType
- RelativePath
- SilentInstallArguments
- SilentUninstallArguments
- RequiresElevation
- SupportedArchitectures[]
- SupportedWindows[]
- UsbVendorIds[]
- HardwareIdPatterns[]
- Sha256
- SignaturePublisher
- SourceUrl
- RedistributionStatus
- LicenseFile
- Priority
- FallbackPackageId
```

`RedistributionStatus`:

```text
Allowed
NotAllowed
Unknown
```

Pacotes `Unknown` ou `NotAllowed` não entram no Setup final.

## 6. Detecção USB

O Bebel consulta o Windows usando:

```text
pnputil /enum-devices /connected
pnputil /enum-drivers
```

e, quando necessário, WMI/SetupAPI para obter:

- Instance ID
- Hardware IDs
- Compatible IDs
- VID
- PID
- classe USB
- fabricante reportado
- nome do dispositivo
- driver provider
- driver version
- driver date
- status/problem code

VID/PID é evidência de transporte, não prova isolada do modelo comercial.

## 7. Diagnóstico ADB

Depois da camada USB:

```text
adb devices -l
```

Estados normalizados:

```text
DRIVER_MISSING
DRIVER_INCORRECT
ADB_UNAUTHORIZED
ADB_OFFLINE
ADB_READY
USB_ONLY
UNKNOWN
```

### DRIVER_MISSING
Windows vê o hardware, mas não há interface ADB/WinUSB funcional e há problema de driver.

### DRIVER_INCORRECT
Existe driver associado, porém a interface esperada não está disponível ou o dispositivo apresenta problema compatível com driver inadequado.

### ADB_UNAUTHORIZED
`adb devices` retorna `unauthorized`.

Mensagem:
```text
Driver funcionando. Autorize a depuração USB na tela do aparelho.
```

Não recomendar reinstalação de driver como primeira ação.

### ADB_OFFLINE
ADB detecta o serial como `offline`.

Ações:
- `adb kill-server`
- reiniciar ADB
- testar cabo/porta
- só oferecer reparo de driver quando a camada Windows indicar problema.

### ADB_READY
ADB retorna `device`.

Nenhuma instalação de driver é necessária.

## 8. Resolução do driver recomendado

Entrada:

```text
UsbDeviceSnapshot
+ ADB state
+ VID/PID
+ Hardware IDs
+ manufacturer
```

Saída:

```text
DriverRecommendation
- PackageId
- Confidence
- Reason
- CurrentState
- Action
```

Confiança:

```text
EXATO
PROVÁVEL
GENÉRICO
```

## 9. Interface

Nova aba:

```text
Drivers USB / ADB
```

Resumo:

```text
Dispositivo: Samsung SM-S921B
Fabricante detectado: Samsung
VID/PID: 04E8 / ....
Driver atual: ausente
ADB: não disponível

Driver recomendado:
Samsung USB Driver
Versão: ...
Assinatura: ...
Status: OFFLINE / disponível no pacote

[ Instalar driver recomendado ]
[ Reparar driver ]
[ Instalar pacote completo ]
[ Reexaminar dispositivo ]
[ Ver todos os drivers ]
```

## 10. Tela “Ver todos os drivers”

Tabela:

```text
Fabricante | Pacote | Versão | Arquitetura | Assinado | Instalado | Ação
```

Ações:

```text
Instalar
Reparar
Abrir pasta
Ver origem/licença
```

Não instalar automaticamente todos os pacotes na inicialização.

## 11. Instalação elevada

A instalação deve ser explicitamente iniciada pelo usuário.

Fluxo:

1. verificar hash do pacote;
2. verificar assinatura;
3. confirmar pacote recomendado;
4. solicitar elevação UAC;
5. executar instalador oficial ou `pnputil /add-driver`;
6. capturar código de saída;
7. reexaminar dispositivo;
8. rodar ADB novamente;
9. apresentar resultado.

Nenhum pacote com hash divergente é executado.

## 12. Instalação por INF

Quando houver INF redistribuível:

```text
pnputil /add-driver "<arquivo.inf>" /install
```

Para pasta:

```text
pnputil /add-driver "<pasta>\*.inf" /subdirs /install
```

Antes:
- validar `.cat`;
- validar assinatura;
- verificar arquitetura/Windows.

## 13. Instaladores EXE/MSI

O manifesto registra apenas argumentos silenciosos verificados para aquela versão.

Se uma versão não tiver modo silencioso documentado/testado, abrir o instalador interativo após consentimento do usuário.

## 14. Reparar driver

“Reparar” não significa remover drivers aleatoriamente.

Fluxo:

1. detectar driver atual;
2. identificar pacote alvo;
3. reinstalar/atualizar o pacote recomendado;
4. reenumerar hardware;
5. reiniciar ADB;
6. confirmar estado.

Remoção explícita com `pnputil /delete-driver` só ocorre quando:
- o pacote OEM correto foi identificado;
- a troca é necessária;
- o usuário confirma;
- não é driver crítico compartilhado.

## 15. Instalar pacote completo

O botão abre uma seleção por fabricante. O usuário escolhe o que instalar.

Não instalar dezenas de drivers silenciosamente sem mostrar seleção.

Depois, exibir relatório de sucesso/falha por pacote.

## 16. Atualização de drivers

Separação:

```text
Catálogos de aparelhos
  -> atualizam diariamente sem novo EXE

Drivers binários
  -> atualizam somente em Release após validação
```

## 17. GitHub Actions

Novo gate:

```text
Validar drivers offline
```

Script:

```text
scripts/validate_driver_packages.py
```

Verificações:

- manifesto JSON válido;
- arquivo declarado existe;
- SHA-256 confere;
- pacote não está `NotAllowed` ou `Unknown`;
- licença/origem registradas;
- não há arquivo não declarado em `Drivers`;
- nenhum segredo/token está presente.

Assinatura Authenticode em runner Windows:

```powershell
Get-AuthenticodeSignature
```

Regras:
- `Status` = `Valid`;
- publisher corresponde ao `SignaturePublisher` esperado.

## 18. Tamanho do Setup

O Setup completo pode crescer significativamente.

A Release mantém:

```text
Bebel-155_V5_2_0.exe
Bebel-155_Setup_V5_2_0.exe
```

O EXE não precisa carregar drivers dentro de resources; o Setup instala os arquivos em `{app}\Drivers`.

## 19. Segurança

O subsistema não:

- baixa driver de sites não oficiais;
- desabilita assinatura de drivers do Windows;
- habilita Test Signing;
- desabilita Secure Boot;
- instala driver unsigned;
- usa pacotes modificados;
- contorna políticas de segurança do Windows.

Se um pacote exige desativar proteções do Windows, ele é rejeitado.

## 20. Logs

Registrar:

```text
data/hora
fabricante
VID/PID
Hardware ID
pacote recomendado
versão
hash verificado
assinatura
comando executado sem segredos
exit code
estado ADB antes/depois
```

Não registrar IMEI ou dados pessoais desnecessários.

## 21. Integração com reconhecimento

Depois de `ADB_READY`:

```text
DeviceDiscovery
-> AndroidProbe
-> DeviceResolver
-> CatalogManager
```

Responsabilidades:

```text
Driver subsystem = conectividade USB/ADB
Recognition subsystem = identidade/especificações
```

## 22. Componentes propostos

```text
Drivers/
  DriverModels.cs
  UsbDriverDiscovery.cs
  DriverResolver.cs
  DriverPackageManager.cs
  DriverInstaller.cs

scripts/
  validate_driver_packages.py

tests/
  DriverTests.cs
  run-driver-tests.ps1
```

## 23. Testes obrigatórios

### Diagnóstico
- Samsung sem ADB driver
- Samsung ADB autorizado
- ADB unauthorized
- ADB offline
- VID desconhecido
- interface genérica WinUSB
- dois aparelhos simultâneos

### Manifesto
- pacote válido
- SHA incorreto
- arquivo ausente
- licença ausente
- redistribuição Unknown
- publisher errado
- assinatura inválida

### Instalação
- INF válido
- instalador EXE válido
- UAC negado
- installer exit code != 0
- reboot requerido
- reparo seguido de ADB_READY

### UI
- recomendar pacote correto
- não recomendar reinstalação em `ADB_UNAUTHORIZED`
- não executar pacote sem consentimento
- relatório de instalação completa

## 24. Critérios de aceitação

O subsistema é considerado pronto somente quando:

1. Samsung e demais marcas suportadas aparecem no manifesto;
2. cada binário embarcado tem origem, licença, SHA e assinatura validados;
3. nenhum pacote com redistribuição não autorizada entra no Setup;
4. o Bebel diferencia problema de driver de autorização ADB;
5. `ADB_READY` não dispara instalação desnecessária;
6. driver recomendado é selecionado por evidência de hardware;
7. instalação pede UAC apenas quando iniciada pelo usuário;
8. falha de instalação é exibida, não mascarada;
9. Setup funciona totalmente offline;
10. a Release Windows valida todos os pacotes antes de publicar.
