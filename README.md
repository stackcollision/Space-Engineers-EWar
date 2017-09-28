# Space-Engineers-EWar

This project is a mod for space engineers which adds radar and electronic countermeasures to the game.

## :large_blue_circle: Radar 

### :large_blue_circle: Type: Volume Search 

Search radar provides a live picture of what is going on around you.  These radars only cover the 90x90 degree sector of the sky that they are pointed at, meaning that 8 are required for full coverage of the space around you.  

In order to support a tradeoff between range and resolution (discussed below), there are two types of volume search radar blocks.  One provides long range low resolution scans, and the other provides short range high resolution to catch those pesky fighters trying to sneak in close.

Every radar track appears as a beacon on your HUD.  To support realism the name and type of the grid is not displayed.  Tracks are identified by a number, with their radar cross-section size shown next to it.  For example, "Track 32873 (412m²)"

### :red_circle: Type: Fire Control

This is a possible future addition, which would require that special radars be present in order for turrets to be able to track targets.  This would drastically change how weapons work, so it may not be implemented at all.

### :large_blue_circle: Cross-Sections and Resolution

To prevent radar from being overpowered this mod calculates the cross-section of grids in range to determine if they can be detected by a radar.  Every radar has a resolution which determines the minimum size grid it can detect at a given range.  This minumum size increases linearly as the distance from the radar increases.  A small 30 m² fighter will only be detectable within 1000m of the radar, while a frigate might be detectable up to 10km away and a carrier up to 40km.

The cross-section of a grid is also not constant.  As the ship turns relative to the radar the cross-section will change.  Pointing the broad side of your ship towards the enemy will produce a stronger radar return, making you detectable out to a longer range.

### :large_blue_circle: Physical Obstructions

Radar requires line-of-sight in order to work.  Hiding in the radiation shadow of asteroids, behind mountains, or even behind other grids can obscure you from detection.

## :red_circle: Radiation Detectors 

Radars work by emitting radio energy into space and listening for a return signal.  This energy can be detected by anyone with the proper equipment at up to twice the range of the transmitting radar.  Ships with Direction Finding antenna blocks pointing at the correct area of space will be able to see where radar is emanating from.  They will not see exact coordinates, only a direction to look in.

## :red_circle: Jamming 

At its most basic level, jamming works by screaming louder than the radar so it can't hear its own echo.  There are currently two types of jammers planned for this mod.

### :red_circle: Omni-directional 

This is a simple countermeasure which requires no attention.  It jams incoming radar signals from all directions.  Because it must work in all directions it can be gradually overcome as the radar gets closer.  Once the radar's power exceeds the power of the jammer your enemy will have a fix on you.

### :red_circle: Focused 

A much more power jammer is mounted on a turret, which can be steered to an area of the sky to provide a powerful jamming beam.  This beam prevents the affected radar from getting a fix on *any* other ships in your area, meaning you can protect your allies.

## :red_circle: Radiation-Absorbant Tiles

These tiles are a special, expensive type of armor block which can be placed on the exterior layer of your ship to artificially reduce your radar cross-section, making your ship harder to detect.

## :red_circle: Chaff 

Chaff launchers work in a similar way to the Decoy block.  When you launch a chaff round it will travel a short distance from your ship and then explode into a cloud of small decoys, distracting enemy turrets from shooting your ship for a short time.
