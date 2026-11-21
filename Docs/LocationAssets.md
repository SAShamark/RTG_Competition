# Flame & Blame — моделі для локації

Корінь паку: `Assets/Plugins/polyperfect/Low Poly Ultimate Pack/` (далі `LPUP`).
Префаби шукати через Project → пошук за назвою. Власні префаби: `Assets/Contents/Prefabs/`.

## 1. Вулиця (сцени 01, 02, 07–09)
| Що | Префаб |
|---|---|
| Пожежна машина (лом у відсіку) | `Contents/Prefabs/firetruck` |
| Поліція / швидка для фону | `Contents/Prefabs/car-police`, `car-ambulance-pickup` |
| Фасад багатоповерхівки | `building-block-5floor-front`, `building-block-5floor-corner`, `building-block-5floor-short` (+ `-apocalypse` для обгорілого фіналу) |
| Сусідні будинки фоном | `building-block-4floor-*` |
| Тротуар / дорога | `tile-sidewalk-*`, `floor-concrete-3x3m` |
| Гідрант | `fire-hydrant` |
| Лавка (записка з кодом) | `bench-old` / `bench-garden` |
| Ліхтарі | `lamp-city` |
| Дерева, кущі, клумба (вогник 1%) | `tree`, `bush-medium`, `farm-flower-bed`, `shrub-flowers` |
| Паркан | `fence-picket`, `fence-classic` |
| Смітник | `bin-wheelie`, `trash-can` |
| Пожежники-NPC фоном | `man-fire` |
| «Герой» у майці | `man-beach` (запасний `man-casual`) |

## 2. Під'їзд і коридор (сцени 02, 03)
| Що | Префаб |
|---|---|
| Стіни / двері / вікна 3×3 | `wall-stone-3x3-*`, `wall-stone-door-3x3m-*`, `wall-stone-window-3x3m-*`, кути `wall-stone-corner-*` |
| Підлога | `floor-concrete-3x3m`, `floor-stone-3x3m-a` |
| Вхідні двері | `door-house-window`, `door-wood` |
| Сходи | `stairs-house-4m`, `stairs-house-l-4m` (+ `railing-stairs-stone`) |
| Пожежний шланг на стіні | `wallbox-hose` |
| Вогнегасник | `fire-extinguisher` |
| Світло | `lamp-ceiling` |
| Сміття, відра | `bucket-construction`, `broom`, `crate-box` |

## 3. Квартира 1 — бабуся і шафа (сцена 04)
| Що | Префаб |
|---|---|
| Стіни / підлога | `wall-wood-*-3x3m`, `floor-parquet-a-3x3m` |
| Шафа | `closet-wardrobe-closed` |
| Комод, що блокує шафу | `cabinet-kitchen-drawer` (або `cabinet-bathroom-long` як заглушка) |
| Меблі | `sofa`, `chair-dining-old`, `bed-big-wood`, `clock-home`, `mirror-tall`, `carpet`, `rug-circle` |
| Що падає й б'ється | `pot-medium-circle-plant`, `pot-small-circle-cactus-*`, `pot-tiny-succulent-*`, `plate-round`, `books-pile`, `picture-frame` |
| Бабуся | `woman-nanny` (запасна `woman-lady`) |

## 4. Квартира 2 — кухня і газ (сцена 05)
| Що | Префаб |
|---|---|
| Плита | `cooker` / `oven` |
| Холодильник (закриває вентиль) | `fridge` |
| Кухонні шафи | `cabinet-kitchen`, `cabinet-kitchen-top`, `cabinet-kitchen-large`, `rail-kitchen-utensil` |
| Що випадає | `pot`, `pan`, `kettle`, `bowl-salad`, `coffee-cup`, ножі `knife-kitchen-*` |
| Газова труба | `pipe-*` з паку (+ власний вентиль, див. п. 7) |
| Щиток із замком (альтернативне рішення) | `box-cabinet-closed` |
| Мешканець | `man-sweater` |

## 5. Майданчик 2-го поверху та обвал (сцена 06)
| Що | Префаб |
|---|---|
| Балки | `beam-wood`, `beam-large`, `steel-beam` |
| Завал | `rubble-apocalypse-a…f`, `rubble-big-apocalypse`, `rubble-concrete-apocalypse-a`, `brick-*` |
| Потріскана стіна | `wall-stone-3x3-*` + декаль тріщин (нарізка на шматки — власна) |
| Кувалда | `sledgehammer` |
| Ванна сусідньої квартири (обхідний шлях) | `bath`, `toilet`, `cabinet-bathroom-sink`, `mirror` |
| Третій мешканець | `woman-casual` |

## 6. Вогонь, дим, анімації
| Що | Джерело |
|---|---|
| Вогонь (заглушка) | `LPUP/fire.prefab`, `fireplace` |
| Дим | `LPUP/- Particles/particles_smoke` |
| Анімації людей | `LPUP/- Animations/People Animation/`: idle, walk, run, sitting |

## 7. Чого немає в паку і що з цим робити
| Потреба | Рішення | Посилання |
|---|---|---|
| **Стилізований вогонь** | ✅ Full Opaque Fire — безкоштовний, лише URP, протестований на Unity 6, low-poly | https://assetstore.unity.com/packages/vfx/full-opaque-fire-312221 |
| **Дим** | ✅ Free Stylized Smoke Effects Pack — безкоштовний, Particle System | https://assetstore.unity.com/packages/vfx/particles/fire-explosions/free-stylized-smoke-effects-pack-226406 |
| **Коти** | Безкоштовного живого кота на Asset Store немає. Варіанти: безкоштовний Animated Cat SET на CGTrader (royalty-free) або LOW POLY CATS – Animated за $4.99 | https://www.cgtrader.com/free-3d-models/animal/other/low-poly-animated-cat-set-game-ready · https://assetstore.unity.com/packages/3d/characters/animals/mammals/low-poly-cats-animated-290005 |
| **Лом** | Безкоштовний лом на Asset Store реалістичний, у 4K PBR і лише Built-in — не підходить за стилем. Краще зробити з примітивів у Unity (3 хв роботи) | https://assetstore.unity.com/packages/3d/props/tools/crowbar-free-wrecking-bar-low-poly-4k-285620 |
| **Вентиль, ланцюг із замком, домофон** | Зробити з примітивів (циліндр + тор, куби) у кольорах атласу LPUP | — |
| **Руки пожежника від першої особи (див. також п. 8)** | Безкоштовних low-poly на Asset Store не знайшлося. Рукавиці з примітивів у кольорах `man-fire`, або itch.io «First Person Hands with Gloves» (pay what you want) | https://rrfreelance.itch.io/first-person-hands-with-gloves |

## 8. Що ще можна взяти (дослідження, 17.11.2026)
Ціни й ліцензії взято зі сторінок станом на 17.11.2026 — перевіряти перед імпортом.

### Коти
| Пак | Ціна / ліцензія | Нотатки |
|---|---|---|
| Quaternius — Animated Animals Low Poly (кіт, пес, вовк...) | безкоштовно, CC0 | https://opengameart.org/content/animated-animales-low-poly — пак 2017 року, низькополігональний, найкраще пасує до polyperfect |
| Kenney — Cube Pets (є кіт) | безкоштовно, CC0 | кубічний стиль, анімований |
| Animated Cat SET (CGTrader) | безкоштовно, royalty-free | https://www.cgtrader.com/free-3d-models/animal/other/low-poly-animated-cat-set-game-ready |
| LOW POLY CATS – Animated | $4.99, Asset Store | https://assetstore.unity.com/packages/3d/characters/animals/mammals/low-poly-cats-animated-290005 |

### Персонажі й анімації
| Пак | Ціна / ліцензія | Нотатки |
|---|---|---|
| Quaternius — Universal Animation Library 1 і 2 | безкоштовно (Standard), CC0 | 120+ і 130+ анімацій на гуманоїдному ризі: штовхання, повзання, біг, сидіння. Ретаргет на ріг polyperfect через Humanoid. https://quaternius.itch.io/universal-animation-library |
| Low Poly People (David Jalbert) | безкоштовно на Asset Store, URP | 18 персонажів різної статури, з ригом і анімаціями (за даними itch.io). https://assetstore.unity.com/packages/3d/characters/humanoids/low-poly-people-by-david-jalbert-274814 |
| Synty Sidekick Modular Characters Starter | безкоштовно | модульні персонажі в стилі POLYGON, інший стиль, ніж polyperfect |
| Low Poly Fire Fighter | $9.99 | https://assetstore.unity.com/packages/3d/characters/low-poly-fire-fighter-210951 |

### Інтер'єр
| Пак | Ціна / ліцензія | Нотатки |
|---|---|---|
| Kenney — Furniture Kit | безкоштовно, CC0 | ~140 моделей, є кухня, ванна, вітальня. https://kenney.nl/assets/furniture-kit |
| Low Poly Simple Furniture FREE | безкоштовно, URP | 44 моделі. https://assetstore.unity.com/packages/3d/props/furniture/low-poly-simple-furniture-free-240197 |
| KayKit Furniture Bits | €5.51 | яскравий «іграшковий» стиль, ближчий до мультяшних концептів |

## 9. Обрано (17.11.2026)
- **Меблі:** Low Poly Simple Furniture FREE (Asset Store) + Kenney Furniture Kit (CC0); polyperfect як база.
- **Персонажі:** Synty Sidekick Modular Characters Starter; запасний варіант — Low Poly People (David Jalbert); додаткові анімації — Mixamo.
- **Коти:** Animated Cat SET (CGTrader, безкоштовний).
- **Вогонь і дим:** пізніше.
