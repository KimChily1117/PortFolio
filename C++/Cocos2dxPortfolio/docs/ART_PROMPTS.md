# ASTER 캐릭터 생성 기록

생성 도구: built-in imagegen. CLI/API fallback을 사용하지 않았습니다. 결과는 프로젝트 Resources/art에 복사했습니다.

## 캐릭터 기본 시트

파일: `Resources/art/characters.png`, 실제 크기 1536×1024 RGBA. 3열 2행, 영웅 3종과 적 3종.

Use case: stylized-concept. Asset type: production transparent character sprite atlas for an original mobile idle fantasy RPG called ASTER / Idle Expedition. One single image exactly 1536 by 1024 pixels, perfectly regular 3-column by 2-row grid, six equal 512x512 cells. Transparent alpha background everywhere. No grid lines, no text, no labels, no watermark, no floor, no shadows outside characters. Each cell contains one separate fully visible whole-body chibi anime fantasy character centered with 50 px padding and feet at same cell baseline, same scale, ready for Cocos2d-x Sprite atlas slicing. Top row left: original ivory-haired female knight with ivory navy armor, teal scarf, elegant long silver sword facing right; top middle: original pink-haired mage with plum outfit gold trim crystal staff facing right; top right: original dark-blue-haired male ranger with teal cloak and compact bow facing right. Bottom row left: teal spectral armored wolf facing left; bottom middle: violet hooded small crystal sorcerer enemy facing left; bottom right: imposing original ivory horned stone golem with amber glowing cracks facing left, fits entire cell. Hand-painted clean game sprite style, clear silhouettes, crisp outlines, beautiful detailed fabric and metal, restrained celestial teal / ivory / lavender palette. All original designs not from any existing franchise. Exactly six isolated characters and no decorative extras. All remain inside their own cells.

## 영웅 애니메이션 시트

파일: `Resources/art/heroes-animation.png`, 실제 크기 1448×1086 RGBA. 요청 해상도와 다르게 생성되어 실행 시 실제 텍스처 크기를 4열 3행으로 나눕니다. 각 셀은 362×362입니다. 기본 시트의 영웅을 참조했습니다.

Create a NEW transparent animation atlas using ONLY the three ORIGINAL friendly characters in the TOP ROW of the supplied reference image. Preserve each hero's appearance, costume, hair color and hand-painted chibi anime style exactly. Asset for a Cocos2d-x mobile game, not an illustration. Output exactly 2048x1536 pixels with a rigorous 4-column by 3-row grid of 512x512 cells. Row1 all four cells: ivory-haired knight with teal scarf and silver sword, facing RIGHT. Row2 all four cells: pink-haired purple mage with staff, facing RIGHT. Row3 all four cells: blue-haired teal ranger with bow, facing RIGHT. Each row is the SAME character in four distinct animation frames: column1 neutral ready standing; column2 gentle breathing idle cloth and hair motion; column3 clear dramatic ATTACK pose (knight sword slash forward, mage casting staff forward, ranger firing bow); column4 attack recovery pose. All full bodies must fit entirely inside their individual 512x512 cell, with AT LEAST 65 pixels empty transparent margin on ALL sides, including weapons and hair. Feet at exactly cell y=440, same character size and alignment across every frame. No pixels from one character may enter adjacent cells. True transparent alpha background with no painted background, no ground, no shadow, no ambient glow outside the body, no grids, no writing, no UI, no extra characters. Treat this as a precise sprite animation atlas.

프레임은 대기 2장·공격·복귀를 재생합니다. 생성 이미지의 포즈·발 위치와 셀 경계는 수작업 아트 정리로 더 다듬을 수 있습니다.
