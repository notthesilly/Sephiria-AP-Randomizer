from BaseClasses import Location


class SephiriaLocation(Location):
    game = "Sephiria"


shrine_check_counts = {
    1: 15,
    2: 20,
    3: 25,
    4: 25,
    5: 25,
    6: 30,
}


chapter_floor_counts = {
    1: 3,
    2: 4,
    3: 5,
    4: 5,
    5: 5,
    6: 6,
}


floor_names = {
    1: "Mole Stronghold",
    2: "Woodlands",
    3: "Library",
    4: "Wasteland",
    5: "Rooted Cave",
    6: "Qliphoth's Temple",
}


late_floor_start_by_goal_chapter = {
    1: 3,  # Library
    2: 4,  # Wasteland
    3: 5,  # Rooted Cave
    4: 5,  # Rooted Cave
    5: 5,  # Rooted Cave
    6: 5,  # Rooted Cave
}


early_destiny_inscription_candidates = [
    "Destiny Inscription - Tablet",
    "Destiny Inscription - Rylie's Gift",
    "Destiny Inscription - Heavy Weapon Training",
    "Destiny Inscription - Nimble Weapon Training",
    "Destiny Inscription - Dice I",
    "Destiny Inscription - Home Repairs",
    "Destiny Inscription - Dice Acquisition",
    "Destiny Inscription - Seed Money",
    "Destiny Inscription - Ranged Weapon Training",
    "Destiny Inscription - Talent Point I",
    "Destiny Inscription - Talent Point II",
    "Destiny Inscription - Level Up Restoration",
    "Destiny Inscription - Increase HP I",
]


late_destiny_inscription_locations = {
    "Destiny Inscription - Terrain Recovery",
    "Destiny Inscription - Chest of Possibilities",
    "Destiny Inscription - Basement",
    "Destiny Inscription - Sharp Weapon Training",
    "Destiny Inscription - Graceful Weapon Training",

    "Destiny Inscription - Extra Experience Drop",
    "Destiny Inscription - Talent Point IV",
    "Destiny Inscription - Talent Point V",
    "Destiny Inscription - Dice III",
    "Destiny Inscription - Expanding Pouch II",
    "Destiny Inscription - Side Bag II",

    "Destiny Inscription - Artifact Unlock IV",
    "Destiny Inscription - Artifact Unlock V",
    "Destiny Inscription - Artifact Unlock VI",
    "Destiny Inscription - Artifact Unlock VII",
    "Destiny Inscription - Artifact Unlock VIII",
    "Destiny Inscription - Bond Artifact Unlock",
}


DEFAULT_SEPHIRITE_CHECKS_PER_FLOOR = 20
MAX_SEPHIRITE_CHECKS_PER_FLOOR = 30


def get_sephirite_check_count(
    floor_number,
    sephirites_per_floor=DEFAULT_SEPHIRITE_CHECKS_PER_FLOOR
):
    if floor_number == 6:
        return sephirites_per_floor // 2

    return sephirites_per_floor


shrine_location_table = {
    f"Archipelago Shrine Check {i:02d}": i
    for i in range(1, 31)
}


sephirite_location_table = {}


next_location_id = 1001

for floor_number in range(1, 7):
    floor_name = floor_names[floor_number]

    floor_check_count = get_sephirite_check_count(
        floor_number,
        MAX_SEPHIRITE_CHECKS_PER_FLOOR
    )

    for item_number in range(
        1,
        floor_check_count + 1
    ):
        location_name = (
            f"{floor_name} Sephirite Item "
            f"{item_number}"
        )

        sephirite_location_table[
            location_name
        ] = next_location_id

        next_location_id += 1

costume_mission_location_table = {
    "Costume Mission - Red-Clothed Cat": 2001,
    "Costume Mission - Red Fox": 2002,
    "Costume Mission - Frog": 2003,
    "Costume Mission - Mole": 2004,
    "Costume Mission - Otter": 2005,
    "Costume Mission - White Wolf": 2006,
    "Costume Mission - Wizard Bunny": 2007,
    "Costume Mission - Eagle": 2008,
    "Costume Mission - Turtle": 2013,
    "Costume Mission - Forest Cat": 2014,
    "Costume Mission - Ghost": 2015,
    "Costume Mission - Skeleton": 2017,
    "Costume Mission - Wingless Bat": 2018,
}

destiny_inscription_location_table = {
    # Main / quest nodes
    "Destiny Inscription - Tablet": 3001,
    "Destiny Inscription - Rylie's Gift": 3002,
    "Destiny Inscription - Heavy Weapon Training": 3003,
    "Destiny Inscription - Nimble Weapon Training": 3004,
    "Destiny Inscription - Dice I": 3005,
    "Destiny Inscription - Magic Workshop": 3006,
    "Destiny Inscription - Home Repairs": 3007,
    "Destiny Inscription - Terrain Recovery": 3008,
    "Destiny Inscription - Dice Acquisition": 3009,
    "Destiny Inscription - Seed Money": 3010,
    "Destiny Inscription - Chest of Possibilities": 3011,
    "Destiny Inscription - Ranged Weapon Training": 3012,
    "Destiny Inscription - Basement": 3013,
    "Destiny Inscription - Wishing Fountain": 3014,
    "Destiny Inscription - Dice Transformation": 3015,
    "Destiny Inscription - Sharp Weapon Training": 3016,
    "Destiny Inscription - Hide and Seek": 3017,
    "Destiny Inscription - Graceful Weapon Training": 3018,
    "Destiny Inscription - Fruit Skewer": 3019,

    # Upgrade nodes
    "Destiny Inscription - Talent Point I": 3020,
    "Destiny Inscription - Talent Point II": 3021,
    "Destiny Inscription - Level Up Restoration": 3022,
    "Destiny Inscription - Dice II": 3023,
    "Destiny Inscription - Expanding Pouch I": 3024,
    "Destiny Inscription - Expanding Pouch II": 3025,
    "Destiny Inscription - Extra Experience Drop": 3026,
    "Destiny Inscription - Talent Point III": 3027,
    "Destiny Inscription - Talent Point IV": 3028,
    "Destiny Inscription - MP Regeneration": 3029,
    "Destiny Inscription - Talent Point V": 3030,
    "Destiny Inscription - Increase HP I": 3031,
    "Destiny Inscription - Increase HP II": 3032,
    "Destiny Inscription - Dice III": 3033,
    "Destiny Inscription - Side Bag I": 3034,
    "Destiny Inscription - Side Bag II": 3035,

    # Artifact nodes
    "Destiny Inscription - Artifact Unlock I": 3036,
    "Destiny Inscription - Artifact Unlock II": 3037,
    "Destiny Inscription - Artifact Unlock III": 3038,
    "Destiny Inscription - Artifact Unlock IV": 3039,
    "Destiny Inscription - Artifact Unlock V": 3040,
    "Destiny Inscription - Artifact Unlock VI": 3041,
    "Destiny Inscription - Bond Artifact Unlock": 3042,
    "Destiny Inscription - Artifact Unlock VII": 3043,
    "Destiny Inscription - Artifact Unlock VIII": 3044,
}

# -------------------------------------------------
# Mission completion checks
# -------------------------------------------------

mission_location_table = {
    "Mission - Home Repairs": 3501,
    "Mission - Fruit Skewer": 3502,
    "Mission - Magic Workshop": 3503,
    "Mission - Basement": 3505,
    "Mission - Hide and Seek": 3506,
    "Mission - Rylie's Gift": 3507,
    "Mission - Terrain Recovery": 3508,
}

boss_defeat_location_table = {
    "Boss Defeat - Mogdi": 4001,
    "Boss Defeat - Rataka": 4002,
    "Boss Defeat - Erma": 4003,
    "Boss Defeat - Askard": 4004,
    "Boss Defeat - Odner": 4005,
    "Boss Defeat - Lerid": 4006,
    "Boss Defeat - Pentaxis": 4007,
    "Boss Defeat - Kovess": 4008,
    "Boss Defeat - Kraz": 4009,
    "Boss Defeat - Haz": 4010,
}


boss_defeat_floor_table = {
    "Boss Defeat - Mogdi": 1,
    "Boss Defeat - Odner": 1,
    "Boss Defeat - Haz": 1,

    "Boss Defeat - Rataka": 2,
    "Boss Defeat - Lerid": 2,

    "Boss Defeat - Erma": 3,
    "Boss Defeat - Pentaxis": 3,

    "Boss Defeat - Askard": 4,

    "Boss Defeat - Kovess": 5,
    "Boss Defeat - Kraz": 5,
}

location_table = {
    **shrine_location_table,
    **sephirite_location_table,
    **costume_mission_location_table,
    **destiny_inscription_location_table,
    **mission_location_table,
    **boss_defeat_location_table
}