# Guia do Iniciante – Criando seu RPG com este Projeto

Este guia mostra, passo a passo, como você pode usar o projeto para criar seu RPG. A UI, cena principal (Main Scene) e multiplayer já estão prontos. Você só precisa cuidar de:
 - Tokens
 - Subscenes (suas cenas de mapa/ambiente)
 
 Este guia completo ensina desde importar sprites até criar o Token Setup, salvar o prefab na pasta certa e habilitar suas cenas.
 
## Sobre o projeto (Nexus)

- Nome do projeto: Nexus.
- O que é: kit/boilerplate para mesa virtual de RPG com tokens 2.5D, UI pronta e multiplayer (Mirror).
- O que já vem pronto: MainScene, UI de Tokens (Tab), UI de Cenas (T), integração de rede e spawn sincronizado.

## Pastas que você vai usar

- `Assets/NexusUser/Resources/Tokens/...` – ONDE SALVAR os prefabs dos seus tokens (use subpastas como categorias). Recomendado para manter separado do núcleo.
- Obs: A busca por Resources é global, então `Assets/Nexus/Resources/Tokens/...` e `Assets/Resources/Tokens/...` também funcionam; porém usamos `NexusUser` para facilitar upgrades.
- `Assets/NexusUser/Scenes/Subscenes/...` – suas cenas de mapa/ambiente.
- `Assets/Scenes/MainScene.unity` – cena principal (já pronta).
- Dica: Menu Tools > Nexus > User Folder > Create Structure cria toda a estrutura `Assets/NexusUser` e já cria `DemoToken` (em `NexusUser/Resources/Tokens/Demo`) e `DemoScene` (em `NexusUser/Scenes/Subscenes`).

## Fazer os Tokens aparecerem

0) Importe seu sprite (PNG):
   - Arraste o arquivo para a pasta do projeto.
   - No Inspector: Texture Type = Sprite (2D and UI). Ajuste Pixels Per Unit conforme seu padrão.

1) Crie o GameObject base do Token (recomendado – 1 clique):
   - Menu: GameObject > Nexus > Create Token.
   - Isso cria um objeto com `TokenSetup`, um filho `Sprite` com `SpriteRenderer` e JÁ adiciona: `BoxCollider`, `DragObjectOnGround`, `NetworkIdentity` e `NetworkedToken` (pronto para multiplayer).
   - Selecione o `SpriteRenderer` e arraste seu sprite para o campo Sprite.

2) Complete o mínimo do prefab:
   - Ajuste o `BoxCollider` se necessário (um padrão já é criado).
   - Multiplayer já está pronto (componentes de rede são adicionados automaticamente).

3) Salve como Prefab na pasta CORRETA:
   - `Assets/NexusUser/Resources/Tokens/<Categoria opcional>/MeuToken.prefab`
   - As subpastas viram categorias na UI (ex.: `Tokens/Monstros/Undead`).


4) No jogo, pressione Tab para abrir a UI de Tokens:
   - Procure pelo nome, clique no card para spawnar.
   - Se não aparecer, clique em Refresh (na UI) e confirme o caminho acima.


## Controles e Keybinds

- UI de Tokens: Tab (abre/fecha). Clique para spawnar.
- UI de Cenas: T (abre/fecha).
- Câmera (Player): WASD move, segure o Botão Direito do Mouse para olhar/rotacionar, Espaço sobe, Left Ctrl desce, Q/E inclinam (roll), Shift+Scroll ajusta velocidade, Scroll ajusta zoom.
- Tokens (arrastar simples no chão):
  - Clique e arraste com o botão esquerdo do mouse para mover pelo plano.
  - O token ajusta a altura automaticamente (snap) ao terreno/escadas.
- Cursor: sempre livre (não trava). Arrastar tokens com clique esquerdo não foca o mouse.
- Focados no Token (TokenSetup):
  - Olhe para o token (centro da tela sobre ele) e use 1-6 para trocar sprites/variações.
  - Teclas + e - mudam a escala.


## Colocar suas Subscenes (mapas)

1) Crie suas cenas e salve em `Assets/NexusUser/Scenes/Subscenes/`.

2) Adicione TODAS as cenas ao Build Settings:
   - File > Build Settings > Add Open Scenes (inclua MainScene e suas subscenes).

3) Sobre o `SceneConfig`:
   - O utilitário cria automaticamente `Assets/NexusUser/Resources/SceneConfig.asset` e o vincula na MainScene.
   - Ele também registra suas subscenes deste diretório no Build Settings e no `SceneConfig`.


5) No jogo, pressione T para abrir a UI de Cenas e clique para carregar sua subscene.
  - A `DemoScene` criada pelo utilitário já é adicionada ao Build Settings e ao `SceneConfig` automaticamente.


## Pronto (resumo)

- Tokens: prefabs em `Assets/NexusUser/Resources/Tokens/...` → Tab para spawnar.
- Cenas: subscenes em `Assets/NexusUser/Scenes/Subscenes`, adicione ao Build Settings, configure no `SceneConfig` → T para carregar.


## Problemas comuns (rápido)

- Token não aparece na lista: verifique se o prefab está em `Assets/NexusUser/Resources/Tokens/...` (ou qualquer `Resources/Tokens`), use Refresh e confirme que há um `TokenLibraryManager` na MainScene.
- Cena não aparece na UI: confira se está no Build Settings e se o `sceneName` no `SceneConfig` está idêntico.

## Estrutura separada para upgrades

- Ao atualizar o Nexus, substitua apenas o núcleo (ex.: `Assets/Nexus` e dependências). Seus conteúdos ficam em `Assets/NexusUser` e permanecem intactos.
- Ferramentas úteis: Tools > Nexus > User Folder (criar estrutura, mover selecionados e migrar tokens padrão).
