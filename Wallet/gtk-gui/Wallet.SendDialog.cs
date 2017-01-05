
// This file has been generated by the GUI designer. Do not modify.
namespace Wallet
{
	public partial class SendDialog
	{
		private global::Gtk.VBox vbox1;
		
		private global::Gtk.HBox hbox5;
		
		private global::Gtk.Label label4;
		
		private global::Gtk.Label labelHeader;
		
		private global::Gtk.EventBox eventboxCancel;
		
		private global::Gtk.Image image5;
		
		private global::Gtk.Image imageCurrency;
		
		private global::Gtk.HBox hboxContent;
		
		private global::Wallet.SendDialogWaiting senddialogwaiting;
		
		private global::Wallet.SendDialogStep1 senddialogstep1;
		
		private global::Wallet.SendDialogStep2 senddialogstep2;
		
		private global::Gtk.HBox hboxFooter;
		
		private global::Gtk.Label labelBalance;
		
		private global::Gtk.Label labelFee;

		protected virtual void Build ()
		{
			global::Stetic.Gui.Initialize (this);
			// Widget Wallet.SendDialog
			global::Stetic.BinContainer.Attach (this);
			this.WidthRequest = 650;
			this.Name = "Wallet.SendDialog";
			// Container child Wallet.SendDialog.Gtk.Container+ContainerChild
			this.vbox1 = new global::Gtk.VBox ();
			this.vbox1.Name = "vbox1";
			this.vbox1.Spacing = 20;
			this.vbox1.BorderWidth = ((uint)(15));
			// Container child vbox1.Gtk.Box+BoxChild
			this.hbox5 = new global::Gtk.HBox ();
			this.hbox5.Name = "hbox5";
			// Container child hbox5.Gtk.Box+BoxChild
			this.label4 = new global::Gtk.Label ();
			this.label4.WidthRequest = 50;
			this.label4.Name = "label4";
			this.hbox5.Add (this.label4);
			global::Gtk.Box.BoxChild w1 = ((global::Gtk.Box.BoxChild)(this.hbox5 [this.label4]));
			w1.Position = 0;
			w1.Expand = false;
			w1.Fill = false;
			// Container child hbox5.Gtk.Box+BoxChild
			this.labelHeader = new global::Gtk.Label ();
			this.labelHeader.Name = "labelHeader";
			this.labelHeader.LabelProp = global::Mono.Unix.Catalog.GetString ("Send");
			this.hbox5.Add (this.labelHeader);
			global::Gtk.Box.BoxChild w2 = ((global::Gtk.Box.BoxChild)(this.hbox5 [this.labelHeader]));
			w2.Position = 1;
			// Container child hbox5.Gtk.Box+BoxChild
			this.eventboxCancel = new global::Gtk.EventBox ();
			this.eventboxCancel.Name = "eventboxCancel";
			// Container child eventboxCancel.Gtk.Container+ContainerChild
			this.image5 = new global::Gtk.Image ();
			this.image5.WidthRequest = 50;
			this.image5.Name = "image5";
			this.image5.Pixbuf = global::Stetic.IconLoader.LoadIcon (this, "gtk-dialog-error", global::Gtk.IconSize.Menu);
			this.eventboxCancel.Add (this.image5);
			this.hbox5.Add (this.eventboxCancel);
			global::Gtk.Box.BoxChild w4 = ((global::Gtk.Box.BoxChild)(this.hbox5 [this.eventboxCancel]));
			w4.Position = 2;
			w4.Expand = false;
			w4.Fill = false;
			this.vbox1.Add (this.hbox5);
			global::Gtk.Box.BoxChild w5 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hbox5]));
			w5.Position = 0;
			w5.Expand = false;
			w5.Fill = false;
			// Container child vbox1.Gtk.Box+BoxChild
			this.imageCurrency = new global::Gtk.Image ();
			this.imageCurrency.Name = "imageCurrency";
			this.vbox1.Add (this.imageCurrency);
			global::Gtk.Box.BoxChild w6 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.imageCurrency]));
			w6.Position = 1;
			w6.Expand = false;
			w6.Fill = false;
			// Container child vbox1.Gtk.Box+BoxChild
			this.hboxContent = new global::Gtk.HBox ();
			this.hboxContent.Name = "hboxContent";
			this.hboxContent.Spacing = 6;
			// Container child hboxContent.Gtk.Box+BoxChild
			this.senddialogwaiting = new global::Wallet.SendDialogWaiting ();
			this.senddialogwaiting.Events = ((global::Gdk.EventMask)(256));
			this.senddialogwaiting.Name = "senddialogwaiting";
			this.hboxContent.Add (this.senddialogwaiting);
			global::Gtk.Box.BoxChild w7 = ((global::Gtk.Box.BoxChild)(this.hboxContent [this.senddialogwaiting]));
			w7.Position = 0;
			w7.Expand = false;
			w7.Fill = false;
			// Container child hboxContent.Gtk.Box+BoxChild
			this.senddialogstep1 = new global::Wallet.SendDialogStep1 ();
			this.senddialogstep1.Events = ((global::Gdk.EventMask)(256));
			this.senddialogstep1.Name = "senddialogstep1";
			this.hboxContent.Add (this.senddialogstep1);
			global::Gtk.Box.BoxChild w8 = ((global::Gtk.Box.BoxChild)(this.hboxContent [this.senddialogstep1]));
			w8.Position = 1;
			// Container child hboxContent.Gtk.Box+BoxChild
			this.senddialogstep2 = new global::Wallet.SendDialogStep2 ();
			this.senddialogstep2.Events = ((global::Gdk.EventMask)(256));
			this.senddialogstep2.Name = "senddialogstep2";
			this.hboxContent.Add (this.senddialogstep2);
			global::Gtk.Box.BoxChild w9 = ((global::Gtk.Box.BoxChild)(this.hboxContent [this.senddialogstep2]));
			w9.Position = 2;
			w9.Expand = false;
			w9.Fill = false;
			this.vbox1.Add (this.hboxContent);
			global::Gtk.Box.BoxChild w10 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hboxContent]));
			w10.Position = 2;
			w10.Expand = false;
			w10.Fill = false;
			// Container child vbox1.Gtk.Box+BoxChild
			this.hboxFooter = new global::Gtk.HBox ();
			this.hboxFooter.Name = "hboxFooter";
			this.hboxFooter.Homogeneous = true;
			this.hboxFooter.Spacing = 6;
			// Container child hboxFooter.Gtk.Box+BoxChild
			this.labelBalance = new global::Gtk.Label ();
			this.labelBalance.Name = "labelBalance";
			this.labelBalance.Xalign = 0F;
			this.labelBalance.LabelProp = global::Mono.Unix.Catalog.GetString ("Balance");
			this.hboxFooter.Add (this.labelBalance);
			global::Gtk.Box.BoxChild w11 = ((global::Gtk.Box.BoxChild)(this.hboxFooter [this.labelBalance]));
			w11.Position = 0;
			// Container child hboxFooter.Gtk.Box+BoxChild
			this.labelFee = new global::Gtk.Label ();
			this.labelFee.Name = "labelFee";
			this.labelFee.Xalign = 1F;
			this.labelFee.LabelProp = global::Mono.Unix.Catalog.GetString ("Fee");
			this.hboxFooter.Add (this.labelFee);
			global::Gtk.Box.BoxChild w12 = ((global::Gtk.Box.BoxChild)(this.hboxFooter [this.labelFee]));
			w12.Position = 1;
			this.vbox1.Add (this.hboxFooter);
			global::Gtk.Box.BoxChild w13 = ((global::Gtk.Box.BoxChild)(this.vbox1 [this.hboxFooter]));
			w13.Position = 3;
			w13.Expand = false;
			w13.Fill = false;
			this.Add (this.vbox1);
			if ((this.Child != null)) {
				this.Child.ShowAll ();
			}
			this.Hide ();
		}
	}
}