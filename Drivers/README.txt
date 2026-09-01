BEBEL 155 - FONTES E STATUS DE REDISTRIBUICAO DOS DRIVERS

Regra do projeto:
Somente binarios com redistribuicao explicitamente permitida podem entrar no Setup.
Quando a permissao nao e clara, o manifesto permanece Unknown e nenhum binario e incluido.

Samsung
- Fonte oficial: https://developer.samsung.com/android-usb-driver
- Versao observada em 01/09/2026: 1.9.5.0
- Status no manifesto: NotAllowed
- Motivo: o EULA Samsung Developer limita o uso ao licenciado e proibe transferir o software a terceiros sem consentimento previo por escrito.
- O Bebel pode detectar Samsung e indicar a fonte oficial, mas nao redistribui o instalador dentro do Setup sem autorizacao.

Google USB Driver
- Fonte oficial: https://developer.android.com/studio/run/win-usb
- Status no manifesto: NotAllowed
- Motivo: o Android SDK License Agreement concede licenca nao sublicenciavel e nao transferivel; o pacote exige aceitacao dos termos.
- O Bebel nao repacota esse ZIP como se tivesse direito de sublicenciar.

Demais fabricantes
- Status inicial: Unknown
- Fontes oficiais estao registradas em drivers-manifest.json.
- Cada pacote deve passar por revisao individual de licenca, SHA-256 e Authenticode antes de ser marcado Allowed.

Observacao:
Microsoft WinUSB faz parte do Windows, mas nao e um instalador OEM universal que possa substituir com seguranca todos os drivers de fabricantes.
