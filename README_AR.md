
# AWR Battle Arena - Gameplay Complete Code Only v3

هذه نسخة لعبة كاملة بالكود فقط، بدون ملفات FBX ثقيلة.

## الموجود
- Lobby داخل اللعبة
- اختيار خريطة
- اختيار مود: Respawn / Rounds / MiniBR
- اختيار فرق من الكيبورد
- خرائط كاملة: Warehouse / City / Harbor
- حركة لاعب كاملة
- Sprint / Crouch / Prone / Jump
- TPS / FPS
- Rifle / Pistol / Shotgun / Sniper
- ADS
- Reload
- قنابل
- Bots AI
- Health / Armor / Resources
- Loot
- Respawn
- Round scoring
- Mini Battle Royale Zone
- Spectator
- KillCam placeholder
- HUD
- Kill feed
- أزرار جوال قابلة للسحب والحفظ
- Settings menu
- Graphics Low / Medium / Ultra
- Blood ON/OFF بشكل بسيط
- شخصية مؤقتة إذا لم تضف FBX

## التشغيل
1. افتح Unity 2022.3 LTS أو أحدث.
2. افتح Scene فارغ.
3. أنشئ GameObject باسم AWR_Game.
4. أضف السكربت:
   Assets/AWR_Battle_Arena/AllInOne/AWR_BattleArena_GameplayComplete_v3.cs
5. اضغط Play.

## مكان الشخصية
ضع FBX هنا:
Assets/YourAssets/Character_Put_FBX_Here/

داخل Unity:
Rig > Animation Type: Humanoid
Avatar Definition: Create From This Model
Apply

ثم اسحب Prefab الشخصية إلى:
External Character Prefab

## مكان الأنيميشن
ضع Animation FBX هنا:
Assets/YourAssets/Animations_Put_FBX_Here/

لكل أنيميشن:
Rig > Animation Type: Humanoid
Avatar Definition: Copy From Other Avatar
Source: Avatar حق الشخصية
Apply

## Animator Parameters المطلوبة

Float:
- Speed
- DirectionX
- DirectionY

Bool:
- Sprint
- Crouch
- Prone
- Aim
- Dead

Trigger:
- Fire
- Reload
- Throw
- Hit
- Death
- SwitchWeapon

## الرفع إلى GitHub
هذه النسخة كود فقط، ترفع عاديًا بدون Git LFS.
إذا أضفت ملفات FBX كبيرة، استخدم Git LFS.
