﻿using System;
using Wallet.core.Data;

namespace Wallet
{
	[System.ComponentModel.ToolboxItem (true)]
	public partial class KeysDialog : DialogBase
	{
		private Gtk.ListStore _Store;

		public KeysDialog ()
		{
			this.Build ();

			InitKeysPane (treeview1);

			buttonClose.Clicked += delegate { 
				CloseDialog(); 
			};

			buttonSign.Clicked += delegate { 
				//CloseDialog(); 
				new SignMessageDialog(Selection).ShowDialog();
			};

			//buttonAdd.Clicked += delegate
			//{
			//	new AddKeyDialog(delegate {
			//		App.Instance.Wallet.Import();
			//	}).ShowDialog();
			//};
		}

		private Key Selection { 
			get {
				Gtk.TreeIter iter;
				if (treeview1.Selection.GetSelected (out iter)) {
					return _Store.GetValue (iter, 0) as Key;
				} else {
					return null;
				}
			}
		}

		private void InitKeysPane(Gtk.TreeView treeView, bool? used = null, bool? isChange = null)
		{
			_Store = new Gtk.ListStore(typeof(Key), typeof(string), typeof(string), typeof(string), typeof(string));
			
			treeView.Selection.Mode = Gtk.SelectionMode.Single;

			treeView.Model = _Store;
			treeView.AppendColumn("Public",  new Gtk.CellRendererText(), "text", 1);
			treeView.AppendColumn("Private", new Gtk.CellRendererText(), "text", 2);
			treeView.AppendColumn("Used?",   new Gtk.CellRendererText(), "text", 3);
			treeView.AppendColumn("Change?", new Gtk.CellRendererText(), "text", 4);

			Populate(treeView, used, isChange);
		}

		private void Populate(Gtk.TreeView treeView, bool? used = null, bool? isChange = null)
		{
			foreach (var key in App.Instance.Wallet.GetKeys())
			{
				_Store.AppendValues(key, key.Address.ToString(), DisplayKey(key.Private), key.Used ? "Yes" : "No", key.Change ? "Yes" : "No");
			};
		}

		private String DisplayKey(byte[] key)
		{
			return key == null ? "" : System.Convert.ToBase64String(key);//.Substring (0, 15
		}
	}
}
