
// This file has been generated by the GUI designer. Do not modify.
namespace Wallet
{
	public partial class MainWindow
	{
		private global::Gtk.VBox vbox1;

		private global::Wallet.MainMenu MainMenu1;

		private global::Gtk.HBox hbox6;

		private global::Wallet.MainArea mainarea1;

		protected virtual void Build()
		{
			global::Stetic.Gui.Initialize(this);
			// Widget Wallet.MainWindow
			this.Name = "Wallet.MainWindow";
			this.Title = global::Mono.Unix.Catalog.GetString("ZEN Wallet");
			this.WindowPosition = ((global::Gtk.WindowPosition)(4));
			this.AllowShrink = true;
			// Container child Wallet.MainWindow.Gtk.Container+ContainerChild
			this.vbox1 = new global::Gtk.VBox();
			// Container child vbox1.Gtk.Box+BoxChild
			this.MainMenu1 = new global::Wallet.MainMenu();
			this.MainMenu1.Events = ((global::Gdk.EventMask)(256));
			this.MainMenu1.Name = "MainMenu1";
			this.vbox1.Add(this.MainMenu1);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.vbox1[this.MainMenu1]));
			w1.Position = 0;
			w1.Expand = false;
			// Container child vbox1.Gtk.Box+BoxChild
			this.hbox6 = new global::Gtk.HBox();
			this.hbox6.Name = "hbox6";
			// Container child hbox6.Gtk.Box+BoxChild
			this.mainarea1 = new global::Wallet.MainArea();
			this.mainarea1.Events = ((global::Gdk.EventMask)(256));
			this.mainarea1.Name = "mainarea1";
			this.hbox6.Add(this.mainarea1);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.hbox6[this.mainarea1]));
			w2.Position = 0;
			this.vbox1.Add(this.hbox6);
			global::Gtk.Box.BoxChild w3 = ((global::Gtk.Box.BoxChild)(this.vbox1[this.hbox6]));
			w3.Position = 1;
			this.Add(this.vbox1);
			if ((this.Child != null))
			{
				this.Child.ShowAll();
			}
			this.DefaultWidth = 936;
			this.DefaultHeight = 936;
			this.Show();
			this.DeleteEvent += new global::Gtk.DeleteEventHandler(this.OnDeleteEvent);
		}
	}
}
