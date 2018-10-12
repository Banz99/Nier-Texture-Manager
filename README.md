# Nier-Texture-Manager-PS3
This project is created to extract and repack textures from the game, rearranging pointers in the internal files whenever needed. Please keep in mind that repacking textures with resolution different from the original, while supported, will likely cause the game to crash or not load properly.

Uses LZO decompression library from: http://www.oberhumer.com/opensource/lzo/ and the C# port located at: http://wallaceturner.com/lzo-for-c.

Untiling algorithm for Xbox360 textures borrowed from GTA IV Xbox 360 Texture Editor at http://forum.xentax.com/blog/?p=302.

CTX1 to RGBA conversion adapted in C# from Xenia Emulator at https://github.com/xenia-project/xenia/blob/master/src/xenia/gpu/texture_conversion.cc.
