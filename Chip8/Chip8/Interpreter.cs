/**
 * Chip8 XNA: a Chip-8 interpreter for XBOX/PC
 * Copyright (C) 2012 Cedric Van Goethem
 * 
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Affero General Public License as
 * published by the Free Software Foundation, either version 3 of the
 * License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Affero General Public License for more details.
 * 
 * You should have received a copy of the GNU Affero General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;

using System.IO;
using System.Diagnostics;
using System.Threading;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Audio;

namespace Chip8
{
    public class Interpreter : IDrawable
    {
        private const ushort BaseMemory = 0x200; // was an old size of bootloader

        private const byte LowWidth = 64;
        private const byte LowHeight = 32;
        private const byte HighWidth = 128;
        private const byte HighHeight = 64;

        private static byte[] fontmap = {0xF0, 0x90, 0x90, 0x90, 0xF0,//0
                          0x20, 0x60, 0x20, 0x20, 0x70,//1
                          0xF0, 0x10, 0xF0, 0x80, 0xF0,//2
                          0xF0, 0x10, 0xF0, 0x10, 0xF0,//3
                          0x90, 0x90, 0xF0, 0x10, 0x10,//4
                          0xF0, 0x80, 0xF0, 0x10, 0xF0,//5
                          0xF0, 0x80, 0xF0, 0x90, 0xF0,//6
                          0xF0, 0x10, 0x20, 0x40, 0x40,//7
                          0xF0, 0x90, 0xF0, 0x90, 0xF0,//8
                          0xF0, 0x90, 0xF0, 0x10, 0xF0,//9
                          0xF0, 0x90, 0xF0, 0x90, 0x90,//A
                          0xE0, 0x90, 0xE0, 0x90, 0xE0,//B
                          0xF0, 0x80, 0x80, 0x80, 0xF0,//C
                          0xE0, 0x90, 0x90, 0x90, 0xE0,//D
                          0xF0, 0x80, 0xF0, 0x80, 0xF0,//E
                          0xF0, 0x80, 0xF0, 0x80, 0x80};//F

        public const byte PixelSize = 4;
        private static Color PixelColor = Color.White;

        private const byte TimerTick = 16; //ms
        private int timerCounter = 0;

        private Texture2D pixel;
        private SpriteFont font;
        private SoundEffect beep;
        private SoundEffectInstance soundEngine;

        private static Random randomizer = new Random();

        public string Path { get; private set; }

        // Screen stuff
        public DisplayMode Mode { get; private set; }
        public byte Width { get; private set; }
        public byte Height { get; private set; }

        public bool DebugMode { get; set; }
        public bool Mindfuck { get; set; }

        // Events
        public event EventHandler OnScreenSizeChanged;

        private BitArray screen;

        private byte[] memory;
        private bool running;
        private bool stopped;

        private ushort I; //address register
        private ushort pc; //program counter (x86 EIP)
        private ushort[] stack = new ushort[16]; //stack
        private ushort sp = 0; //stack pointer
        private byte[] v = new byte[16]; //registers
        private byte dt = 0; // delay timer
        private byte st = 0; // sound timer
        private bool[] keys = new bool[16];

        // Timing stuff
        private int spentMiliseconds;
        private uint ticksPerSecond;
        private uint ticks;

        public Interpreter(string path)
        {
            Path = path;
        }

        private void LoadMemory()
        {
            byte[] instructions = File.ReadAllBytes(Path);
            memory = new byte[0x1000];
            Buffer.BlockCopy(instructions, 0, memory, BaseMemory, instructions.Length);
            Buffer.BlockCopy(fontmap, 0, memory, 0, fontmap.Length);
            pc = BaseMemory;
        }

        public void Load(GraphicsDevice g, ContentManager content)
        {
            LoadMemory();
            ChangeDisplayMode(DisplayMode.Small); // init screen
            pixel = new Texture2D(g, PixelSize, PixelSize);
            Color[] data = new Color[PixelSize * PixelSize];
            for (byte i = 0; i < PixelSize * PixelSize; ++i)
                data[i] = PixelColor;
            pixel.SetData(data);

            font = content.Load<SpriteFont>("Fonts/Font");
            beep = content.Load<SoundEffect>("Sound/beep");
            soundEngine = beep.CreateInstance();
        }

        public void Restart()
        {
            if (!running && !stopped)
                return;

            Stop();
            while (!stopped) ;
            stopped = false;
            ClearBuffers();
            Start();
        }

        private void ClearBuffers()
        {
            Clear();
            stack = new ushort[16];
            v = new byte[16];
            keys = new bool[16];
            I = 0;
            sp = 0;
            dt = 0;
            st = 0;
            pc = BaseMemory;
        }

        public void Stop()
        {
            running = false;
        }

        public void Start()
        {
            if (!running)
            {
                running = true;
                new Thread(() => Loop()).Start();
            }
        }

        private void Loop()
        {
            byte A, B, C, D, AB, CD;
            while (running)
            {
                ++ticks;
#if DEBUG
                Thread.Sleep(DebugMode ? 100 : 1);
#else
                if(DebugMode)
                    Thread.Sleep(1);
#endif

                AB = memory[pc];
                CD = memory[pc+1];
                pc += 2;

                A = (byte)(AB >> 4);
                B = (byte)(AB & 0xF);
                C = (byte)(CD >> 4);
                D = (byte)(CD & 0xF);

                switch (A)
                {
                    case 0x00:
                        switch (B)
                        {
                            case 0x00:
                                switch (C)
                                {
                                    case 0x0C:
                                        Debug.WriteLine("Scroll screen down {0} lines.", D);
                                        break;
                                    case 0x0E:
                                        switch (D)
                                        {
                                            case 0x00:
                                                Clear();
                                                Debug.WriteLine("Cleared screen.");
                                                break;
                                            case 0x0E:
                                                pc = stack[--sp];
                                                Debug.WriteLine("Popping stack");
                                                break;
                                            default:
                                                Unk(AB, CD);
                                                break;
                                        }
                                        break;

                                    case 0x0F:
                                        switch (D)
                                        {
                                            case 0x0B:
                                                Debug.WriteLine("Scroll screen 4 right."); //TODO
                                                break;
                                            case 0x0C:
                                                Debug.WriteLine("Scroll screen 4 left."); //TODO
                                                break;
                                            case 0x0E:
                                                ChangeDisplayMode(DisplayMode.Small);
                                                Debug.WriteLine("Changed to SMALL screen mode.");
                                                break;
                                            case 0x0F:
                                                ChangeDisplayMode(DisplayMode.Big);
                                                Debug.WriteLine("Changed to BIG screen mode.");
                                                break;
                                            
                                            default:
                                                Unk(AB, CD);
                                                break;
                                        }
                                        break;

                                    default:
                                        Unk(AB, CD);
                                        break;
                                }
                                break;

                            default:
                                Unk(AB, CD);
                                break;
                        }
                        break;
                    case 0x01:
                        pc = (ushort)((B << 8) | CD);
                        Debug.WriteLine("Jmp to {0}", pc);
                        break;
                    case 0x02:
                        stack[sp++] = pc;
                        pc = (ushort)((B << 8) | CD);
                        Debug.WriteLine("Calling function at {0} and putting current pc on stack.", pc);
                        break;
                    case 0x03:
                        if (v[B] == CD) pc += 2;
                        Debug.WriteLine("Skipping next instruction when {0} == {1}", v[B], CD);
                        break;
                    case 0x04:
                        if (v[B] != CD) pc += 2;
                        Debug.WriteLine("CMP branching: {0} == {1}.", v[B], CD);
                        break;
                    case 0x05:
                        switch (D)
                        {
                            case 0x00:
                                if (v[B] == v[C]) pc += 2;
                                break;
                            default:
                                Unk(AB, CD);
                                break;
                        }
                        break;
                    case 0x06:
                        v[B] = CD;
                        Debug.WriteLine("Assigning {0} to register {1}", CD, B);
                        break;
                    case 0x07:
                        v[B] += CD;
                        Debug.WriteLine("Increasing register {0} with {1}", B, CD);
                        break;
                    case 0x08: // mathematical operations
                        switch (D)
                        {
                            case 0x00:
                                v[B] = v[C];
                                Debug.WriteLine("Copying register {0} to {1}.", C, B);
                                break;
                            case 0x01:
                                v[B] |= v[C];
                                Debug.WriteLine("OR register {0} with register {1}.", B, C);
                                break;
                            case 0x02:
                                v[B] &= v[C];
                                Debug.WriteLine("AND register {0} with register {1}.", B, C);
                                break;
                            case 0x03:
                                v[B] ^= v[C];
                                Debug.WriteLine("XOR register {0} with register {1}.", B, C);
                                break;
                            case 0x04:
                                v[0x0F] = (byte)((v[B] + v[C] > byte.MaxValue) ? 1 : 0);
                                v[B] += v[C]; //auto overflows
                                Debug.WriteLine("ADD register {0} with register {1}.", B, C);
                                break;
                            case 0x05:
                                v[0x0F] = (byte)((v[B] > v[C]) ? 1 : 0); // NOT overflow bit
                                v[B] -= v[C];
                                Debug.WriteLine("SUB register {0} with register {1}.", B, C);
                                break;
                            case 0x06:
                                v[0xF] = (byte)(v[B] & 1);
                                v[B] = (byte)(v[B] >> 1);
                                Debug.WriteLine("Shifting register {0} to the right.", B);
                                break;
                            case 0x07:
                                v[0x0F] = (byte)((v[B] < v[C]) ? 1 : 0); // overflow bit
                                v[B] -= v[C];
                                Debug.WriteLine("SUBN register {0} with register {1}.", B, C);
                                break;
                            case 0x0E:
                                v[0xF] = (byte)((v[B] >> 7) & 1); //extract most significant bit
                                v[B] = (byte)(v[B] << 1); //shift register one to the left
                                Debug.WriteLine("Shifting register {0} to the left.", B);
                                break;
                            default:
                                Unk(AB, CD);
                                break;
                        }
                        break;
                    case 0x09:
                        switch (D)
                        {
                            case 0x00:
                                if (v[B] != v[C]) pc += 2;
                                Debug.WriteLine("Skipping next instruction when register {0} != {1}.", B, C);
                                break;
                            default:
                                Unk(AB, CD);
                                break;
                        }
                       
                        break;
                    case 0x0A:
                        I = (ushort)((B << 8) | CD);
                        Debug.WriteLine("Assigning {0} to I", I);
                        break;
                    case 0x0C:
                        v[B] = (byte)(randomizer.Next(byte.MaxValue) & CD);
                        Debug.WriteLine("Randomized {0} to register {1}",v[B], B);
                        break;
                    case 0x0D:
                        //DrawSprite(V[B], V[C], D, Memory + I);
                        ToggleSprite(v[B], v[C], D);
                        Debug.WriteLine("Drawing at {0}:{1}", v[B], v[C]);
                        break;
                    case 0x0E:
                        switch (CD)
                        {
                            case 0xA1:
                                if (!keys[v[B]]) pc += 2;
                                Debug.WriteLine("Skipping if key {0} was pressed.", v[B]);
                                break;
                            case 0x9E:
                                if (keys[v[B]]) pc += 2;
                                Debug.WriteLine("Skipping if key {0} was not pressed.", v[B]);
                                break;
                            default:
                                Unk(AB, CD);
                                break;
                        }
                        break;
                    case 0x0F:
                        switch (CD)
                        {
                            case 0x07:
                                v[B] = dt;
                                Debug.WriteLine("Placed delay timer in register {0}.", B);
                                break;
                            case 0x0A:
                                Debug.WriteLine("Waiting for key input...");
                                WaitForKeypress(B);
                                break;
                            case 0x15:
                                dt = v[B];
                                Debug.WriteLine("Assigning register {0} to delay timer.", B);
                                break;
                            case 0x18:
                                st = v[B];
                                Debug.WriteLine("Assigning register {0} to sound timer.", B);
                                break;
                            case 0x1E:
                                I += v[B];
                                Debug.WriteLine("Increased I with {0} from register {1}.", v[B], B);
                                break;
                            case 0x29:
                                I = (ushort)(5 * v[B]);
                                Debug.WriteLine("Moving I to font buffer {0}.", B);
                                break;
                            case 0x30:
                                //TODO: this has to be a higher pixel (font drawer)
                                I = (ushort)(5 * v[B]);
                                break;
                            case 0x33:
                                byte value = v[B];
                                memory[I] = (byte)(value / 100);
                                memory[I + 1] = (byte)((value - (memory[I] * 100)) / 10);
                                memory[I + 2] = (byte)(value - ((memory[I] * 100) + (memory[I + 1] * 10)));
                                Debug.WriteLine("Stored register {0} to {1}-{2}-{3} array in memory at {4}.",
                                    B, memory[I], memory[I + 1], memory[I + 2], I);  
                                break;
                            case 0x55:
                                CopyRegisters(B);
                                Debug.WriteLine("Copying register 0-{0} to memory {1}.", B, I);
                                break;
                            case 0x65:
                                ReadMemory(B);
                                Debug.WriteLine("Reading memory at {0} to register 0-{1}.", I, B);
                                break;
                            default:
                                Unk(AB, CD);
                                break;
                        }
                        break;
                    default:
                        Unk(AB, CD);
                        break;
                }
            }
            stopped = true;
        }

        private void WaitForKeypress(byte register)
        {
            // Next we start searching the key
            byte found = 0xff;
            while (found == 0xff)
            {
                for (byte keyIterator = 0; keyIterator < 16; ++keyIterator)
                {
                    if (keys[keyIterator])
                    {
                        found = keyIterator;
                        break;
                    }
                }
                Thread.Sleep(1); //prevents it from going full cpu usage on checking
            }
            v[register] = found;
        }

        public void SetKey(Chip8Keys key, bool value)
        {
            keys[(byte)key] = value;
        }

        private void ReadMemory(byte to)
        {
            for (byte i = 0; i <= to; ++i)
            {
                v[i] = memory[I + i];
            }
        }

        private void CopyRegisters(byte to)
        {
            for (byte i = 0; i <= to; ++i)
            {
                memory[I + i] = v[i];
            }
        }

        private void ToggleSprite(byte x, byte y, byte count)
        {
            bool inverted = false;
            ushort offset = I;
            byte pixels = memory[I];
            byte height = Height;
            y %= height;

            lock (screen)
            {
                for (; count > 0; --count, ++y, pixels = memory[++offset])
                {
                    if (y == height) y = 0;
                    for (byte i = 0; i < 8; ++i)
                    {
                        bool n = ((pixels >> (8 - i)) & 1) == 1; //extract each bit
                        if (n)
                        {
                            int bit_offset = y * Width + ((x + i) % Width);
                            bool p = screen.Get(bit_offset);
                            inverted = inverted || (p && n);
                            screen.Set(bit_offset, p ^= n);
                        } //TODO: check if xor also has to be done when ZERO
                    }
                }
            }
            v[0xF] = (byte)(inverted ? 1 : 0);
        }

        private void Unk(byte AB, byte CD)
        {
#if DEBUG
            Debug.WriteLine("Unknown opcode: 0x{0:X4}", (AB << 8 | CD));
#else
            Console.WriteLine("Unkown opcode: 0x{0:X4}",  (AB << 8 | CD));
#endif
        }

        public void Clear()
        {
            screen.SetAll(false);
        }

        public void ChangeDisplayMode(DisplayMode mode)
        {
            if (Mode != mode || screen == null)
            {
                Mode = mode;
                Width = (mode == DisplayMode.Big) ? HighWidth : LowWidth;
                Height = (mode == DisplayMode.Big) ? HighHeight : LowHeight;

                if (screen != null)
                {
                    lock (screen)
                    {
                        screen = new BitArray(Width * Height);
                    }
                }
                else screen = new BitArray(Width * Height);

                if (OnScreenSizeChanged != null)
                    OnScreenSizeChanged(this, EventArgs.Empty);
            }
        }

        public void Draw(GameTime time, SpriteBatch batch)
        {
            lock (screen)
            {
                for (byte y = 0; y < Height; ++y)
                {
                    for (byte x = 0; x < Width; ++x)
                    {
                        bool val = screen.Get(y * Width + x);
                        if (val)
                        {
                            Color color = Mindfuck ? new Color(randomizer.Next(byte.MaxValue), randomizer.Next(byte.MaxValue), randomizer.Next(byte.MaxValue))
                                : Color.White;
                            batch.Draw(pixel, new Vector2(x * PixelSize, y * PixelSize), color);
                        }
                    }
                }
            }

            int dy = Height * PixelSize + 10;
            batch.DrawString(font, ticksPerSecond + " Mhz", new Vector2(20, dy), Color.White); 
            batch.DrawString(font, "Debug: " + DebugMode.ToString(), new Vector2(100, dy), Color.White);
        }

        public void Update(GameTime time)
        {
            spentMiliseconds += time.ElapsedGameTime.Milliseconds;
            if (spentMiliseconds >= 1000)
            {
                ticksPerSecond = (uint)(ticks / ((float)spentMiliseconds / 1000f)) / 1000000;
                ticks = 0;
                spentMiliseconds -= 1000;
            }

            // Handle timers
            timerCounter += time.ElapsedGameTime.Milliseconds;
            if (timerCounter > TimerTick)
            {
                byte count = (byte)(timerCounter / TimerTick);
                if (dt != 0)
                {
                    if (count > dt)
                        dt = 0;
                    else
                        dt -= count;
                }

                if (st != 0)
                {
                    if (count > st)
                    {
                        st = 0;
                        Beep();
                    }
                    else
                    {
                        st -= count;
                    }
                }
                timerCounter -= TimerTick * count;
            }
        }

        private void Beep()
        {
            Debug.WriteLine("Beep.");
            soundEngine.Play();
        }
    }

    public enum DisplayMode : byte
    {
        Big,
        Small
    }

    public enum Chip8Keys : byte
    {
        Left = 7, //ok
        Right = 8, //ok
        Action = 10,
        Up = 3, //not ok
        Down = 6,
    }

}
