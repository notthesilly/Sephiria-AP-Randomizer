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


SEPHIRITE_CHECKS_PER_FLOOR = 20


def get_sephirite_check_count(floor_number):
    if floor_number == 6:
        return SEPHIRITE_CHECKS_PER_FLOOR // 2

    return SEPHIRITE_CHECKS_PER_FLOOR


shrine_location_table = {
    f"Archipelago Shrine Check {i:02d}": i
    for i in range(1, 31)
}


sephirite_location_table = {}


next_location_id = 1001

for floor_number in range(1, 7):
    floor_name = floor_names[floor_number]

    floor_check_count = get_sephirite_check_count(floor_number)

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
    "Costume Mission - Crocodile": 2009,
    "Costume Mission - Deer": 2010,
    "Costume Mission - Fairy of Fortune": 2011,
    "Costume Mission - Farmer Squirrel": 2012,
    "Costume Mission - Turtle": 2013,
    "Costume Mission - Forest Cat": 2014,
    "Costume Mission - Ghost": 2015,
    "Costume Mission - Scholar Lizard": 2016,
    "Costume Mission - Skeleton": 2017,
    "Costume Mission - Wingless Bat": 2018,
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
    **boss_defeat_location_table
}