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
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Chip8
{
    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class MainGame : Microsoft.Xna.Framework.Game
    {

        private const string Path = @"..\prog\BLINKY";

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Interpreter interpreter;

        private bool debugToggling = false;
        private bool mindFuckToggling = false;
        private bool changeScreenSize = false;

        // Key definitions - currently on azerty scheme
        private const Keys UpKey = Keys.Z; 
        private const Keys DownKey = Keys.S;
        private const Keys LeftKey = Keys.Q;
        private const Keys RightKey = Keys.D;
        private const Keys ActionKey = Keys.A;

        public MainGame()
        {
            graphics = new GraphicsDeviceManager(this);
            // Set resolution
            graphics.PreferredBackBufferHeight = 300;
            graphics.PreferredBackBufferWidth = 600;
            Window.Title = "Chip8 - Cedric Van Goethem";
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            interpreter.Start();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            interpreter = new Interpreter(Path);
            interpreter.OnScreenSizeChanged += new EventHandler(interpreter_OnScreenSizeChanged);

            interpreter.Load(GraphicsDevice, Content);
            ApplyScreenSettings();
        }

        void interpreter_OnScreenSizeChanged(object sender, EventArgs e)
        {
            changeScreenSize = true;
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            interpreter.Stop();
        }

        private void ApplyScreenSettings()
        {
            graphics.PreferredBackBufferHeight = (interpreter.Height * Interpreter.PixelSize) + 40;
            graphics.PreferredBackBufferWidth = interpreter.Width * Interpreter.PixelSize;
            graphics.ApplyChanges();
            changeScreenSize = false;
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (changeScreenSize)
            {
                ApplyScreenSettings();
            }

            KeyboardState state = Keyboard.GetState();
            // Allows the game to exit
            if (state.IsKeyDown(Keys.Escape))
                this.Exit();
            if(state.IsKeyDown(Keys.R))
                interpreter.Restart();

            // Toggle debug on key press
            if (state.IsKeyDown(Keys.T))
                debugToggling = true;
            else if (state.IsKeyUp(Keys.T) && debugToggling)
            {
                interpreter.DebugMode = !interpreter.DebugMode;
                debugToggling = false;
            }

            if (state.IsKeyDown(Keys.M))
                mindFuckToggling = true;
            else if (state.IsKeyUp(Keys.M) && mindFuckToggling)
            {
                interpreter.Mindfuck = !interpreter.Mindfuck;
                mindFuckToggling = false;
            }
            HandleChip8Keys(state);

            interpreter.Update(gameTime);

            base.Update(gameTime);
        }

        private void HandleChip8Keys(KeyboardState state)
        {
            interpreter.SetKey(Chip8Keys.Up, state.IsKeyDown(UpKey));
            interpreter.SetKey(Chip8Keys.Down, state.IsKeyDown(DownKey));
            interpreter.SetKey(Chip8Keys.Left, state.IsKeyDown(LeftKey));
            interpreter.SetKey(Chip8Keys.Right, state.IsKeyDown(RightKey));
            interpreter.SetKey(Chip8Keys.Action, state.IsKeyDown(ActionKey));
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();
            interpreter.Draw(gameTime, spriteBatch);
            spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
