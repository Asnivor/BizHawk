using System;
using System.Collections.Generic;
using OpenTK.Input;

namespace BizHawk.Client.EmuHawk
{
	public class OTK_GamePad
	{
		// Modified OpenTK Gamepad Handler
		// OpenTK v3.x.x.x breaks the original OpenTK.Input.Joystick implementation, but enables OpenTK.Input.Gamepad 
		// compatibility with OSX / linux
		// This should also give us vibration support (if we implement it)

		private static readonly object _syncObj = new object();
		public static List<OTK_GamePad> Devices = new List<OTK_GamePad>();
		private const int MAX_GAMEPADS = 4; //They don't have a way to query this for some reason. 4 is the minimum promised.

		public static void Initialize()
		{
			Devices.Clear();

			lock (_syncObj)
			{
				for (int i = 0; i < MAX_GAMEPADS; i++)
				{
					GamePadState gps = OpenTK.Input.GamePad.GetState(i);
					if (gps.IsConnected)
					{
						Console.WriteLine(string.Format("joydevice index: {0}", i)); //OpenTK doesn't expose the GUID, even though it stores it internally...
						string gpn = OpenTK.Input.GamePad.GetName(i);
						GamePadCapabilities gpc = OpenTK.Input.GamePad.GetCapabilities(i);
						OTK_GamePad ogp = new OTK_GamePad(i, gpn, gpc);
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

		// ********************************** Instance Members **********************************

		readonly Guid _guid;
		readonly int _stickIdx;
		readonly string _name;
		readonly GamePadCapabilities _capabilities;
		GamePadState state = new GamePadState();

		OTK_GamePad(int index, string name, GamePadCapabilities capabilities)
		{
			_guid = Guid.NewGuid();
			_stickIdx = index;
			_name = name;
			_capabilities = capabilities;
			Update();
			InitializeButtons();
			Console.WriteLine("Initialised OpenTK GamePad: " + Name);
			Console.WriteLine("OpenTK Mapping: " + _name);
		}

		public void Update()
		{
			state = OpenTK.Input.GamePad.GetState(_stickIdx);
		}

		public IEnumerable<Tuple<string, float>> GetFloats()
		{			
			yield return new Tuple<string, float>("LeftThumbX", state.ThumbSticks.Left.X);
			yield return new Tuple<string, float>("LeftThumbY", state.ThumbSticks.Left.Y);
			yield return new Tuple<string, float>("RightThumbX", state.ThumbSticks.Right.X);
			yield return new Tuple<string, float>("RightThumbY", state.ThumbSticks.Right.Y);
			yield return new Tuple<string, float>("LeftTrigger", state.Triggers.Left);
			yield return new Tuple<string, float>("RightTrigger", state.Triggers.Right);
			yield break;
		}

		/// <summary>FOR DEBUGGING ONLY</summary>
		public GamePadState GetInternalState()
		{
			return state;
		}

		public string Name { get { return "Joystick " + _stickIdx + string.Format(" ({0})", _name); } }
		public string ID { get { return (_stickIdx + 1).ToString(); } }
		public Guid Guid { get { return _guid; } }

		/*
		public string ButtonName(int index)
		{
			return names[index];
		}
		public bool Pressed(int index)
		{
			return actions[index]();
		}
		
		public int NumButtons { get; private set; }
		

		private readonly List<string> names = new List<string>();
		private readonly List<Func<bool>> actions = new List<Func<bool>>();
		*/
		public readonly List<ButtonObject> buttonObjects = new List<ButtonObject>();

		void AddItem(string _name, Func<bool> pressed)
		{
			//names.Add(_name);
			//actions.Add(pressed);
			//NumButtons++;

			ButtonObject b = new ButtonObject
			{
				ButtonName = _name,
				ButtonAction = pressed
			};

			buttonObjects.Add(b);
		}

		void InitializeButtons()
		{
			// OpenTK GamePad axis return float values
			const float ConversionFactor = 1.0f / short.MaxValue;
			const float dzp = (short)400 * ConversionFactor;
			const float dzn = (short)-400 * ConversionFactor;
			const float dzt = 0.6f; // (short)10 * ConversionFactor;

			// buttons
			if (_capabilities.HasAButton) AddItem("A", () => state.Buttons.A == ButtonState.Pressed);
			if (_capabilities.HasBButton) AddItem("B", () => state.Buttons.B == ButtonState.Pressed);
			if (_capabilities.HasXButton) AddItem("X", () => state.Buttons.X == ButtonState.Pressed);
			if (_capabilities.HasYButton) AddItem("Y", () => state.Buttons.Y == ButtonState.Pressed);
			if (_capabilities.HasBigButton) AddItem("Guide", () => state.Buttons.BigButton == ButtonState.Pressed);
			if (_capabilities.HasStartButton) AddItem("Start", () => state.Buttons.Start == ButtonState.Pressed);
			if (_capabilities.HasBackButton) AddItem("Back", () => state.Buttons.Back == ButtonState.Pressed);
			if (_capabilities.HasLeftStickButton) AddItem("LeftThumb", () => state.Buttons.LeftStick == ButtonState.Pressed);
			if (_capabilities.HasRightStickButton) AddItem("RightThumb", () => state.Buttons.RightStick == ButtonState.Pressed);
			if (_capabilities.HasLeftShoulderButton) AddItem("LeftShoulder", () => state.Buttons.LeftShoulder == ButtonState.Pressed);
			if (_capabilities.HasRightShoulderButton) AddItem("RightShoulder", () => state.Buttons.RightShoulder == ButtonState.Pressed);

			// dpad
			if (_capabilities.HasDPadUpButton) AddItem("DpadUp", () => state.DPad.Up == ButtonState.Pressed);
			if (_capabilities.HasDPadDownButton) AddItem("DpadDown", () => state.DPad.Down == ButtonState.Pressed);
			if (_capabilities.HasDPadLeftButton) AddItem("DpadLeft", () => state.DPad.Left == ButtonState.Pressed);
			if (_capabilities.HasDPadRightButton) AddItem("DpadRight", () => state.DPad.Right == ButtonState.Pressed);

			// sticks
			if (_capabilities.HasLeftYThumbStick)
			{
				AddItem("LStickUp", () => state.ThumbSticks.Left.Y >= dzp);
				AddItem("LStickDown", () => state.ThumbSticks.Left.Y <= dzn);
			}
			if (_capabilities.HasLeftXThumbStick)
			{
				AddItem("LStickLeft", () => state.ThumbSticks.Left.X <= dzn);
				AddItem("LStickRight", () => state.ThumbSticks.Left.X >= dzp);
			}
			if (_capabilities.HasRightYThumbStick)
			{
				AddItem("RStickUp", () => state.ThumbSticks.Right.Y >= dzp);
				AddItem("RStickDown", () => state.ThumbSticks.Right.Y <= dzn);
			}
			if (_capabilities.HasRightXThumbStick)
			{
				AddItem("RStickLeft", () => state.ThumbSticks.Right.X <= dzn);
				AddItem("RStickRight", () => state.ThumbSticks.Right.X >= dzp);
			}

			// triggers
			if (_capabilities.HasLeftTrigger) AddItem("LeftTrigger", () => state.Triggers.Left > dzt);
			if (_capabilities.HasRightTrigger) AddItem("RightTrigger", () => state.Triggers.Right > dzt);

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

			if (_capabilities.HasLeftVibrationMotor)
				_l = left;
			if (_capabilities.HasRightVibrationMotor)
				_r = right;

			OpenTK.Input.GamePad.SetVibration(_stickIdx, left, right);
		}

		public class ButtonObject
		{
			public string ButtonName;
			public Func<bool> ButtonAction;
		}
	}
}

