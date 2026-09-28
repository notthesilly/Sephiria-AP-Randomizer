from BaseClasses import Region, ItemClassification
from worlds.AutoWorld import World, WebWorld

from .Options import SephiriaOptions

from .Items import (
    SephiriaItem,
    item_table,
    hard_mode_reduction_counts
)

from .Locations import (
    SephiriaLocation,
    get_sephirite_check_count,
    location_table, shrine_check_counts,
    chapter_floor_counts,
    floor_names
)


class SephiriaWebWorld(WebWorld):
    pass


class SephiriaWorld(World):
    game = "Sephiria"
    web = SephiriaWebWorld()

    options_dataclass = SephiriaOptions

    item_name_to_id = item_table
    location_name_to_id = location_table

    def create_regions(self):
        menu = Region(
            "Menu",
            self.player,
            self.multiworld
        )

        self.multiworld.regions.append(menu)

        goal_chapter = self.options.goal_chapter.value

        shrine_count = shrine_check_counts[goal_chapter]

        for i in range(1, shrine_count + 1):
            location_name = (
                f"Archipelago Shrine Check {i:02d}"
            )

            location = SephiriaLocation(
                self.player,
                location_name,
                location_table[location_name],
                menu
            )

            menu.locations.append(location)

        floor_count = chapter_floor_counts[
            goal_chapter
        ]

        for floor_number in range(
            1,
            floor_count + 1
        ):
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

                location = SephiriaLocation(
                    self.player,
                    location_name,
                    location_table[location_name],
                    menu
                )

                menu.locations.append(location)

    def create_items(self):
        goal_chapter = self.options.goal_chapter.value

        shrine_count = shrine_check_counts[goal_chapter]
        floor_count = chapter_floor_counts[goal_chapter]
        sephirite_count = sum(
            get_sephirite_check_count(floor_number)
            for floor_number in range(
                    1,
                    floor_count + 1
                )
            )
        total_location_count = (shrine_count + sephirite_count)

        reduction_pool = []

        for item_name, count in hard_mode_reduction_counts.items():
            reduction_pool.extend(
                [item_name] * count
            )

        self.random.shuffle(reduction_pool)

        # Add every Hard Mode reduction item.
        for item_name in reduction_pool:
            item = SephiriaItem(
                item_name,
                ItemClassification.useful,
                item_table[item_name],
                self.player
            )

            self.multiworld.itempool.append(item)


        if total_location_count < len(reduction_pool):
            raise Exception("Not enough locations for all Hard Mode reduction items.")

        # Fill all remaining locations with Nothing.
        nothing_count = (
            total_location_count -
            len(reduction_pool)
        )

        for _ in range(nothing_count):
            item = SephiriaItem(
                "Nothing",
                ItemClassification.filler,
                item_table["Nothing"],
                self.player
            )

            self.multiworld.itempool.append(item)

    def fill_slot_data(self):
        return {
            "goal_chapter": self.options.goal_chapter.value,
            "required_chapter_clears": self.options.required_chapter_clears.value,
            "unique_weapons_required": bool(
                self.options.unique_weapons_required.value    
            ),
        }
