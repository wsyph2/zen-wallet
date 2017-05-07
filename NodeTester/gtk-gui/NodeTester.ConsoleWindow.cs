
// This file has been generated by the GUI designer. Do not modify.
namespace NodeTester
{
	public partial class ConsoleWindow
	{
		private global::Gtk.VBox vbox1;
		
		private global::Gtk.ScrolledWindow GtkScrolledWindow;
		
		private global::Gtk.TextView textviewConsole;
		
		private global::Gtk.HBox hbox1;
		
		private global::Gtk.Button buttonClear;
		
		private global::Gtk.Button buttonConsoleSettings;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget NodeTester.ConsoleWindow
			this.Name = "NodeTester.ConsoleWindow";
			this.Title = global::Mono.Unix.Catalog.GetString ("Zen Node Tester Console");
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			// Container child NodeTester.ConsoleWindow.Gtk.Container+ContainerChild
			this.vbox1 = new global::Gtk.VBox ();
			this.vbox1.Name = "vbox1";
			this.vbox1.Spacing = 6;
			// Container child vbox1.Gtk.Box+BoxChild
			this.GtkScrolledWindow = new global::Gtk.ScrolledWindow ();
			this.GtkScrolledWindow.Name = "GtkScrolledWindow";
			this.GtkScrolledWindow.ShadowType = ((global::Gtk.ShadowType)(1));
			// Container child GtkScrolledWindow.Gtk.Container+ContainerChild
			this.textviewConsole = new global::Gtk.TextView ();
			this.textviewConsole.CanFocus = true;
			this.textviewConsole.Name = "textviewConsole";
			this.GtkScrolledWindow.Add (this.textviewConsole);
			this.vbox1.Add (this.GtkScrolledWindow);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.GtkScrolledWindow]));
			w2.Position = 0;
			// Container child vbox1.Gtk.Box+BoxChild
			this.hbox1 = new global::Gtk.HBox ();
			this.hbox1.Name = "hbox1";
			this.hbox1.Spacing = 6;
			this.hbox1.BorderWidth = ((uint)(5));
			// Container child hbox1.Gtk.Box+BoxChild
			this.buttonClear = new global::Gtk.Button ();
			this.buttonClear.CanFocus = true;
			this.buttonClear.Name = "buttonClear";
			this.buttonClear.UseUnderline = true;
			this.buttonClear.Label = global::Mono.Unix.Catalog.GetString ("Clear");
			this.hbox1.Add (this.buttonClear);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.buttonClear]));
			w3.Position = 0;
			w3.Expand = false;
			w3.Fill = false;
			// Container child hbox1.Gtk.Box+BoxChild
			this.buttonConsoleSettings = new global::Gtk.Button ();
			this.buttonConsoleSettings.CanFocus = true;
			this.buttonConsoleSettings.Name = "buttonConsoleSettings";
			this.buttonConsoleSettings.UseUnderline = true;
			this.buttonConsoleSettings.Label = global::Mono.Unix.Catalog.GetString ("Settings");
			this.hbox1.Add (this.buttonConsoleSettings);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.hbox1 [this.buttonConsoleSettings]));
			w4.Position = 2;
			w4.Expand = false;
			w4.Fill = false;
			this.vbox1.Add (this.hbox1);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hbox1]));
			w5.Position = 1;
			w5.Expand = false;
			w5.Fill = false;
			this.Add (this.vbox1);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.DefaultWidth = 910;
			this.DefaultHeight = 553;
			this.Show ();
			this.DeleteEvent += new global::Gtk.DeleteEventHandler (this.OnDeleteEvent);
			this.buttonClear.Clicked += new global::System.EventHandler (this.Button_Clear);
		}
	}
}
