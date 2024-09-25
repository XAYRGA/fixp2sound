# fixp2sound
The purpose of this is to fix the broken instruments in Pikmin 2 on the switch. Nintendo shipped them with broken instruments in the audio archive, so it ruins the experience of some levels.

## Why does this bug occur?
This happens because when porting Pikmin 2 to the switch, they didn't rebuild the audio archives, but instead built a whole new piece of software to go through and endian swap the files. This worked ok for the most part, but in some places they forgot to consider that certain bytes might have multiple references -- which ended up in them getting rotated multiple times. If something had an even number of references, it would rotate them back to big endian, when the switch is looking for little endian. Whoops.
![image](https://github.com/user-attachments/assets/34e22db4-fbc5-4f51-9555-36e8ae8384a8)

## How do I use this?
First, you must have a modified switch to do this.

1. Download the latest release.
2. Backup your game, and extract the romfs from it.
3. In the romfs, you'll need to find the "AudioRes" folder.
4. You'll find something called "psound.aaf", drop it onto the fixp2sound.exe
5. It will ask you to confirm, press Y to patch the .aaf
6. Now you'll need to either manually patch the ROM with the new AAF, or if you're running custom firmware, you can drop the .aaf file here.
``SDCARD:\atmosphere\contents\0100D680194B2000\romfs\pikmin2\AudioRes`` (Thanks to [Lunazone](https://github.com/lunazone), she was very helpful with testing and helping with my backup, and providing the cfw path!)


### Expected output
Final process should look like this


![image](https://github.com/user-attachments/assets/689e705e-9aef-466b-bd0e-4c7b4f8c0395)
