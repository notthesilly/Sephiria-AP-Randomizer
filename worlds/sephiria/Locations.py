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


location_table = {
    **shrine_location_table, **sephirite_location_table
}