using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Input;

namespace BizHawk.Client.EmuHawk
{
	public class OTK_GamePad
	{
		// Modified OpenTK Gamepad Handler
		// OpenTK v3.x.x.x breaks the original OpenTK.Input.Joystick implementation, but enables OpenTK.Input.Gamepad 
		// compatibility with OSX / linux. However, the gamepad auto-mapping is a little suspect, so we will have to use both methods
		// This should also give us vibration support (if we ever implement it)

		#region Static Members

		private static readonly object _syncObj = new object();
		private const int MAX_GAMEPADS = 4; //They don't have a way to query this for some reason. 4 is the minimum promised.
		public static List<OTK_GamePad> Devices = new List<OTK_GamePad>();		

		/// <summary>
		/// Initialization is only called once when MainForm loads
		/// </summary>
		public static void Initialize()
		{
			Devices.Clear();

			int playerCount = 0;

			lock (_syncObj)
			{
				for (int i = 0; i < MAX_GAMEPADS; i++)
				{
					if (OpenTK.Input.GamePad.GetState(i).IsConnected || Joystick.GetState(i).IsConnected)
					{
						Console.WriteLine(string.Format("OTK GamePad/Joystick index: {0}", i));
						OTK_GamePad ogp = new OTK_GamePad(i, ++playerCount);
						Devices.Add(ogp);
					}
				}
			}
		}

		public static IEnumerable<OTK_GamePad> EnumerateDevices()
		{
			lock (_syncObj)
			{
				foreach (var device in Devices)
				{
					yield return device;
				}
			}
		}

		public static void UpdateAll()
		{
			lock (_syncObj)
			{
				foreach (var device in Devices)
				{
					device.Update();
				}
			}
		}

		public static void CloseAll()
		{
			if (Devices != null)
			{
				Devices.Clear();
			}
		}

		#endregion

		#region Instance Members

		/// <summary>
		/// The GUID as detected by OpenTK.Input.Joystick
		/// (or auto generated if this failed)
		/// </summary>
		readonly Guid _guid = Guid.NewGuid();

		/// <summary>
		/// Signs whether OpenTK returned a GUID for this device
		/// </summary>
		readonly bool _guidObtained;

		/// <summary>
		/// The OpenTK device index
		/// </summary>
		readonly int _deviceIndex;

		/// <summary>
		/// The index to lookup into Devices
		/// </summary>
		readonly int _playerIndex;

		/// <summary>
		/// The name (if any) that OpenTK GamePad has resolved via its internal mapping database
		/// </summary>
		readonly string _name;

		/// <summary>
		/// The object as returned by OpenTK.Input.Gamepad.GetCapabilities();
		/// </summary>
		readonly GamePadCapabilities? _gamePadCapabilities;

		/// <summary>
		/// Has OpenTK detected the device using the GamePad class?
		/// </summary>
		readonly bool _hasGamepad;

		/// <summary>
		/// The object as returned by OpenTK.Input.Joystick.GetCapabilities();
		/// </summary>
		readonly JoystickCapabilities? _joystickCapabilities;

		/// <summary>
		/// Has OpenTK detected the device using the Joystick class?
		/// </summary>
		readonly bool _hasJoystick;

		/// <summary>
		/// Gamepad Device state information - updated constantly
		/// </summary>
		GamePadState state = new GamePadState();
		GamePadState oldState = new GamePadState();

		/// <summary>
		/// Joystick Device state information - updated constantly
		/// </summary>
		JoystickState jState = new JoystickState();
		JoystickState oldJstate = new JoystickState();

		OTK_GamePad(int index, int playerIndex)
		{
			_deviceIndex = index;
			_playerIndex = playerIndex;

			var gameState = OpenTK.Input.GamePad.GetState(_deviceIndex);
			var joyState = Joystick.GetState(_deviceIndex);

			if (joyState.IsConnected)
			{
				_guid = Joystick.GetGuid(_deviceIndex);
				_guidObtained = true;
				_joystickCapabilities = Joystick.GetCapabilities(_deviceIndex);
			}
			else
			{
				_guid = Guid.NewGuid();
			}

			if (gameState.IsConnected)
			{
				_name = OpenTK.Input.GamePad.GetName(_deviceIndex);
				_gamePadCapabilities = OpenTK.Input.GamePad.GetCapabilities(_deviceIndex);
			}
			else
			{
				_name = "OTK GamePad Undetermined Name";
			}
			
			Update();

			Console.WriteLine("Initialising OpenTK GamePad: " + _guid);
			Console.WriteLine("OpenTK Mapping: " + _name);

			InitializeMappings();			
		}

		public void Update()
		{
			// update both here just in case
			var tmpState = OpenTK.Input.GamePad.GetState(_deviceIndex);
			if (!tmpState.Equals(state))
			{
				state = tmpState;
				DebugGamepadState();
			}
			
			var tmpJstate = Joystick.GetState(_deviceIndex);
			if (!tmpJstate.Equals(jState))
			{
				jState = tmpJstate;
				DebugJoystickState();
			}			
		}

		private void DebugGamepadState()
		{
			Debug.WriteLine("GamePad State:\t" + state.ToString());
		}

		private void DebugJoystickState()
		{
			Debug.WriteLine("Joystick State:\t" + jState.ToString());
		}

		/// <summary>
		/// The things that use GetFloats() (analog controls) appear to require values -10000 to 10000
		/// rather than the -1.0 to 1.0 that OpenTK returns (although even then the results may be slightly outside of these bounds)
		/// Note: is there a better/more perfomant way to do this?
		/// </summary>
		/// <param name="num"></param>
		/// <returns></returns>
		private float SetBounds(float num)
		{
			if (num > 1)
				num = 1;
			if (num < -1)
				num = -1;

			return num * 10000;
		}

		public IEnumerable<Tuple<string, float>> GetFloats()
		{
			if (_gamePadCapabilities.HasValue && _gamePadCapabilities.Value.IsMapped)
			{
				// automapping identified - use OpenTK.Input.GamePad class
				yield return new Tuple<string, float>("LeftThumbX", SetBounds(state.ThumbSticks.Left.X));
				yield return new Tuple<string, float>("LeftThumbY", SetBounds(state.ThumbSticks.Left.Y));
				yield return new Tuple<string, float>("RightThumbX", SetBounds(state.ThumbSticks.Right.X));
				yield return new Tuple<string, float>("RightThumbY", SetBounds(state.ThumbSticks.Right.Y));
				yield return new Tuple<string, float>("LeftTrigger", SetBounds(state.Triggers.Left));
				yield return new Tuple<string, float>("RightTrigger", SetBounds(state.Triggers.Right));
				yield break;
			}
			else
			{
				// use OpenTK.Input.Joystick class
				yield return new Tuple<string, float>("X", SetBounds(jState.GetAxis(0)));
				yield return new Tuple<string, float>("Y", SetBounds(jState.GetAxis(1)));
				yield return new Tuple<string, float>("Z", SetBounds(jState.GetAxis(2)));
				yield return new Tuple<string, float>("W", SetBounds(jState.GetAxis(3)));
				yield return new Tuple<string, float>("V", SetBounds(jState.GetAxis(4)));
				yield return new Tuple<string, float>("S", SetBounds(jState.GetAxis(5)));
				yield return new Tuple<string, float>("Q", SetBounds(jState.GetAxis(6)));
				yield return new Tuple<string, float>("P", SetBounds(jState.GetAxis(7)));
				yield return new Tuple<string, float>("N", SetBounds(jState.GetAxis(8)));
				yield break;
			}
		}

		public string Name { get { return "Joystick " + _playerIndex + string.Format(" ({0})", _name); } }
		public string ID { get { return (_playerIndex).ToString(); } }
		public Guid Guid { get { return _guid; } }

		/// <summary>
		/// Contains name and delegate function for all buttons, hats and axis
		/// </summary>
		public List<ButtonObject> buttonObjects = new List<ButtonObject>();

		void AddItem(string _name, Func<bool> pressed)
		{
			ButtonObject b = new ButtonObject
			{
				ButtonName = _name,
				ButtonAction = pressed
			};

			buttonObjects.Add(b);
		}

		public struct ButtonObject
		{
			public string ButtonName;
			public Func<bool> ButtonAction;
		}

		/// <summary>
		/// Setup mappings prior to button initialization
		/// This is also here in case in the future we want users to be able to supply their own mappings for a device,
		/// perhaps via an input form. Possibly wishful thinking/overly complex.
		/// </summary>
		void InitializeMappings()
		{
			// currently OpenTK has an internal database of mappings for the GamePad class: https://github.com/opentk/opentk/blob/master/src/OpenTK/Input/GamePadConfigurationDatabase.cs
			// if an internal mapping is detected, use that. otherwise, use the joystick class to instantiate the controller
			if (!_gamePadCapabilities.HasValue || !_gamePadCapabilities.Value.IsMapped)
			{
				// no internal map detected - use the joystick class
				InitializeJoystickControls();
			}
			else
			{
				// internal map detected - use the GamePad class
				InitializeGamePadControls();
			}
		}

		string[] aNames = new string[] { "X", "Y", "Z", "AXIS" };

		void InitializeJoystickControls()
		{
			// OpenTK GamePad axis return float values (as opposed to the shorts of SlimDX)
			const float ConversionFactor = 1.0f / short.MaxValue;
			const float dzp = (short)400 * ConversionFactor;
			const float dzn = (short)-400 * ConversionFactor;
			const float dzt = 0.6f;

			// axis		
			AddItem("X+", () => jState.GetAxis(0) >= dzp);
			AddItem("X-", () => jState.GetAxis(0) <= dzn);
			AddItem("Y+", () => jState.GetAxis(1) >= dzp);
			AddItem("Y-", () => jState.GetAxis(1) <= dzn);			
			AddItem("Z+", () => jState.GetAxis(2) >= dzp);
			AddItem("Z-", () => jState.GetAxis(2) <= dzn);
			AddItem("W+", () => jState.GetAxis(3) >= dzp);
			AddItem("W-", () => jState.GetAxis(3) <= dzn);
			AddItem("V+", () => jState.GetAxis(4) >= dzp);
			AddItem("V-", () => jState.GetAxis(4) <= dzn);
			AddItem("S+", () => jState.GetAxis(5) >= dzp);
			AddItem("S-", () => jState.GetAxis(5) <= dzn);
			AddItem("Q+", () => jState.GetAxis(6) >= dzp);
			AddItem("Q-", () => jState.GetAxis(6) <= dzn);
			AddItem("P+", () => jState.GetAxis(7) >= dzp);
			AddItem("P-", () => jState.GetAxis(7) <= dzn);
			AddItem("N+", () => jState.GetAxis(8) >= dzp);
			AddItem("N-", () => jState.GetAxis(8) <= dzn);	// should be enough axis

			// buttons
			for (int i = 0; i < _joystickCapabilities.Value.ButtonCount; i++)
			{
				int j = i;
				AddItem(string.Format("B{0}", i + 1), () => jState.GetButton(j) == ButtonState.Pressed);
			}

			// hats
			for (int i = 0; i < _joystickCapabilities.Value.HatCount; i++)
			{
				JoystickHatState hat = jState.GetHat((JoystickHat)i);
				AddItem(string.Format("POV{0}U", i + 1), () => hat.IsUp);
				AddItem(string.Format("POV{0}D", i + 1), () => hat.IsDown);
				AddItem(string.Format("POV{0}L", i + 1), () => hat.IsLeft);
				AddItem(string.Format("POV{0}R", i + 1), () => hat.IsRight);
			}
		}

		void InitializeGamePadControls()
		{
			// OpenTK GamePad axis return float values (as opposed to the shorts of SlimDX)
			const float ConversionFactor = 1.0f / short.MaxValue;
			const float dzp = (short)400 * ConversionFactor;
			const float dzn = (short)-400 * ConversionFactor;
			const float dzt = 0.6f;

			// buttons
			if (_gamePadCapabilities.Value.HasAButton) AddItem("A", () => state.Buttons.A == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasBButton) AddItem("B", () => state.Buttons.B == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasXButton) AddItem("X", () => state.Buttons.X == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasYButton) AddItem("Y", () => state.Buttons.Y == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasBigButton) AddItem("Guide", () => state.Buttons.BigButton == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasStartButton) AddItem("Start", () => state.Buttons.Start == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasBackButton) AddItem("Back", () => state.Buttons.Back == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasLeftStickButton) AddItem("LeftThumb", () => state.Buttons.LeftStick == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasRightStickButton) AddItem("RightThumb", () => state.Buttons.RightStick == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasLeftShoulderButton) AddItem("LeftShoulder", () => state.Buttons.LeftShoulder == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasRightShoulderButton) AddItem("RightShoulder", () => state.Buttons.RightShoulder == ButtonState.Pressed);

			// dpad
			if (_gamePadCapabilities.Value.HasDPadUpButton) AddItem("DpadUp", () => state.DPad.Up == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasDPadDownButton) AddItem("DpadDown", () => state.DPad.Down == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasDPadLeftButton) AddItem("DpadLeft", () => state.DPad.Left == ButtonState.Pressed);
			if (_gamePadCapabilities.Value.HasDPadRightButton) AddItem("DpadRight", () => state.DPad.Right == ButtonState.Pressed);

			// sticks
			if (_gamePadCapabilities.Value.HasLeftYThumbStick)
			{
				AddItem("LStickUp", () => state.ThumbSticks.Left.Y >= dzp);
				AddItem("LStickDown", () => state.ThumbSticks.Left.Y <= dzn);
			}
			if (_gamePadCapabilities.Value.HasLeftXThumbStick)
			{
				AddItem("LStickLeft", () => state.ThumbSticks.Left.X <= dzn);
				AddItem("LStickRight", () => state.ThumbSticks.Left.X >= dzp);
			}
			if (_gamePadCapabilities.Value.HasRightYThumbStick)
			{
				AddItem("RStickUp", () => state.ThumbSticks.Right.Y >= dzp);
				AddItem("RStickDown", () => state.ThumbSticks.Right.Y <= dzn);
			}
			if (_gamePadCapabilities.Value.HasRightXThumbStick)
			{
				AddItem("RStickLeft", () => state.ThumbSticks.Right.X <= dzn);
				AddItem("RStickRight", () => state.ThumbSticks.Right.X >= dzp);
			}

			// triggers
			if (_gamePadCapabilities.Value.HasLeftTrigger) AddItem("LeftTrigger", () => state.Triggers.Left > dzt);
			if (_gamePadCapabilities.Value.HasRightTrigger) AddItem("RightTrigger", () => state.Triggers.Right > dzt);
		}

		/// <summary>
		/// Sets the gamepad's left and right vibration
		/// We don't currently use this in Bizhawk - do we have any cores that support this?
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		public void SetVibration(float left, float right)
		{
			float _l = 0;
			float _r = 0;

			if (_gamePadCapabilities.Value.HasLeftVibrationMotor)
				_l = left;
			if (_gamePadCapabilities.Value.HasRightVibrationMotor)
				_r = right;

			OpenTK.Input.GamePad.SetVibration(_deviceIndex, left, right);
		}		

		#endregion
	}
}

