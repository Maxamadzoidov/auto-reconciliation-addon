﻿using AutoReconcilationAddon.Models;
using AutoReconcilationAddon.Services;
using SAPbouiCOM.Framework;
using System;
using System.Collections.Generic;

namespace AutoReconcilationAddon
{
	class Program
	{
		static SAPbobsCOM.Company oCom;
		static SAPbobsCOM.Recordset oRS;

		static SAPbouiCOM.EditText oResultEdittext;
		static SAPbobsCOM.BusinessPartners oBP;

		static TransactionService transactionService;
		static ReconciliationService reconciliationService;
		static ReportService reportService;

		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				Application oApp = null;
				if (args.Length < 1)
				{
					oApp = new Application();
				}
				else
				{
					oApp = new Application(args[0]);
				}
				Application.SBO_Application.AppEvent += new SAPbouiCOM._IApplicationEvents_AppEventEventHandler(SBO_Application_AppEvent);
				Application.SBO_Application.ItemEvent += SBO_Application_ItemEvent;

				oCom = (SAPbobsCOM.Company)Application.SBO_Application.Company.GetDICompany();
				oRS = (SAPbobsCOM.Recordset)oCom.GetBusinessObject(SAPbobsCOM.BoObjectTypes.BoRecordset);
				oBP = (SAPbobsCOM.BusinessPartners)oCom.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oBusinessPartners);

				transactionService = new TransactionService();
				reconciliationService = new ReconciliationService(oCom);
				reportService = new ReportService();

				oApp.Run();
			}
			catch (Exception ex)
			{
				System.Windows.Forms.MessageBox.Show(ex.Message);
			}
		}

		private static void SBO_Application_ItemEvent(string FormUID, ref SAPbouiCOM.ItemEvent pVal, out bool BubbleEvent)
		{
			BubbleEvent = true;

			if (pVal.FormTypeEx == "UDO_FT_AutoReconcilation" && pVal.BeforeAction == false && pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_LOAD)
			{
				SAPbouiCOM.Form oForm = Application.SBO_Application.Forms.Item(FormUID);

				oForm.Width = 800;
				oForm.Height = 700;

				SAPbouiCOM.Item oButtonItem = oForm.Items.Add("action", SAPbouiCOM.BoFormItemTypes.it_BUTTON);
				SAPbouiCOM.Button oActionButton = (SAPbouiCOM.Button)oButtonItem.Specific;
				oActionButton.Caption = "Выверка";
				oButtonItem.Left = 150;
				oButtonItem.Top = 105;
				oButtonItem.Width = 70;
				oButtonItem.Height = 20;
				oButtonItem.AffectsFormMode = false;

				SAPbouiCOM.Item oEditTextItem = oForm.Items.Add("resulted", SAPbouiCOM.BoFormItemTypes.it_EDIT);
				oEditTextItem.Left = 10;
				oEditTextItem.Top = 60;
				oEditTextItem.Width = 550;
				oEditTextItem.Height = 300;

				oResultEdittext = (SAPbouiCOM.EditText)oEditTextItem.Specific;

				oForm.Mode = SAPbouiCOM.BoFormMode.fm_OK_MODE;
				oForm.AutoManaged = true;
			}


			if (pVal.FormTypeEx == "UDO_FT_AutoReconcilation" && pVal.BeforeAction == false
				&& pVal.EventType == SAPbouiCOM.BoEventTypes.et_ITEM_PRESSED && pVal.ItemUID == "action")
			{
				SAPbouiCOM.Form oForm = Application.SBO_Application.Forms.Item(FormUID);

				string bpCode = ((SAPbouiCOM.EditText)oForm.Items.Item("20_U_E").Specific).Value;
				string fromDate = ((SAPbouiCOM.EditText)oForm.Items.Item("22_U_E").Specific).Value;
				string toDate = ((SAPbouiCOM.EditText)oForm.Items.Item("23_U_E").Specific).Value;
				string groupsCombo = ((SAPbouiCOM.ComboBox)(oForm.Items.Item("21_U_Cb").Specific)).Value;

				if (!string.IsNullOrEmpty(bpCode) && !string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
				{
					Application.SBO_Application.StatusBar.SetSystemMessage("Аддон начал выверку. Пожалуйста подожите...",
						SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Warning);

					SAPbobsCOM.BusinessPartners oBusinessPartner = (SAPbobsCOM.BusinessPartners)oCom.GetBusinessObject(SAPbobsCOM.BoObjectTypes.oBusinessPartners);
					oBusinessPartner.GetByKey(bpCode);

					string linkedBusinessPartner = oBusinessPartner.LinkedBusinessPartner;
					string xml = "";

					try
					{
						SAPbobsCOM.InternalReconciliationOpenTrans openTransactions =
							reconciliationService.GetOpenTransactions(!(oBusinessPartner.LinkedBusinessPartner == "") ?
							reconciliationService.GetLinkedTransParams(bpCode, linkedBusinessPartner, fromDate, toDate) :
							reconciliationService.GetTransParams(bpCode, fromDate, toDate));


						xml = openTransactions.ToXMLString();
						transactionService.ClearTransactions();

						foreach (SAPbobsCOM.InternalReconciliationOpenTransRow reconciliationOpenTransRow in
							(SAPbobsCOM.IInternalReconciliationOpenTransRows)openTransactions.InternalReconciliationOpenTransRows)

							transactionService.AddTransaction(
								new Transaction(reconciliationOpenTransRow.TransId, reconciliationOpenTransRow.TransRowId,
								reconciliationOpenTransRow.CreditOrDebit != 0,
								reconciliationOpenTransRow.ReconcileAmount));


						transactionService.AutoReconciliate();
						Dictionary<(int, int), double> reconciliatedTransactions = transactionService.GetReconciliatedTransactions();
						reconciliationService.Reconciliate(openTransactions, reconciliatedTransactions);

						Application.SBO_Application.StatusBar.SetSystemMessage("Выверка для [" + bpCode + "] и [" + linkedBusinessPartner + "] успешно завершена.\r\n",
							SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Success);

						Application.SBO_Application.StatusBar.SetSystemMessage("Автоматическая выверка успешно завершена.",
							SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Success);

					}
					catch (Exception ex)
					{
						Console.WriteLine(ex.Message);
					}
				}

				else if (!string.IsNullOrEmpty(groupsCombo) && !string.IsNullOrEmpty(fromDate) && !string.IsNullOrEmpty(toDate))
				{
					Console.WriteLine("Addon is working");

					Application.SBO_Application.StatusBar.SetSystemMessage("Аддон начал выверку. Пожалуйста подожите...",
						SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Warning);

					oRS.DoQuery($"SELECT \"CardCode\" FROM OCRD WHERE \"CardType\" = '{groupsCombo}'");

					List<Partner> list = new List<Partner>();
					for (int i = 0; i < oRS.RecordCount; i++)
					{
						var bpcode = oRS.Fields.Item("CardCode").Value.ToString();

						Partner partner = new Partner() { CardCode = bpcode };
						list.Add(partner);

						oRS.MoveNext();
					}

					foreach (var item in list)
					{
						Console.WriteLine(item.CardCode);
					}

					bool flag = false;
					oResultEdittext.Value = "";

					foreach (var bomboRow in list)
					{
						oBP.GetByKey(bomboRow.CardCode);
						string cardCode = bomboRow.CardCode;
						string linkedBusinessPartner = oBP.LinkedBusinessPartner;
						string xml = "";
						transactionService.ClearTransactions();

						try
						{
							SAPbobsCOM.InternalReconciliationOpenTrans openTransactions = reconciliationService.GetOpenTransactions(!(linkedBusinessPartner == "") ?
								reconciliationService.GetLinkedTransParams(cardCode, linkedBusinessPartner, fromDate, toDate) :
								reconciliationService.GetTransParams(cardCode, fromDate, toDate));

							xml = openTransactions.ToXMLString();

							foreach (SAPbobsCOM.InternalReconciliationOpenTransRow reconciliationOpenTransRow
								in openTransactions.InternalReconciliationOpenTransRows)

								transactionService.AddTransaction(
									new Transaction(reconciliationOpenTransRow.TransId, reconciliationOpenTransRow.TransRowId,
									reconciliationOpenTransRow.CreditOrDebit != 0, reconciliationOpenTransRow.ReconcileAmount));

							transactionService.AutoReconciliate();
							Dictionary<(int, int), double> reconciliatedTransactions = transactionService.GetReconciliatedTransactions();
							reconciliationService.Reconciliate(openTransactions, reconciliatedTransactions);

							string str = oResultEdittext.Value;
							oResultEdittext.Value = "Выверка для [" + cardCode + "] успешно завершена." + Environment.NewLine + str + Environment.NewLine;
						}
						catch (Exception ex)
						{
							flag = true;
							string str = oResultEdittext.Value;
							oResultEdittext.Value = string.Format("Ошибка при выверке для [{0}]: {1}\r\n", (object)bomboRow.CardCode, (object)ex) + str;
							reportService.ReportError(cardCode, linkedBusinessPartner, ex, xml);
						}
					}
					if (!flag)
						Application.SBO_Application.StatusBar.SetText("Автоматическая выверка успешно завершена.", Type: SAPbouiCOM.BoStatusBarMessageType.smt_Success);
					else
						Application.SBO_Application.StatusBar.SetText("Произошла ошибка при выверке некторых бизнес-партнеров. Просмотрите логи. Процесс выверки успешно завершена.", Type: SAPbouiCOM.BoStatusBarMessageType.smt_Success);
				}
				else
				{
					Application.SBO_Application.StatusBar.SetSystemMessage("Заполните все поля.", SAPbouiCOM.BoMessageTime.bmt_Short, SAPbouiCOM.BoStatusBarMessageType.smt_Error);
				}
			}
		}

		static void SBO_Application_AppEvent(SAPbouiCOM.BoAppEventTypes EventType)
		{
			switch (EventType)
			{
				case SAPbouiCOM.BoAppEventTypes.aet_ShutDown:
					//Exit Add-On
					System.Windows.Forms.Application.Exit();
					break;
				case SAPbouiCOM.BoAppEventTypes.aet_CompanyChanged:
					break;
				case SAPbouiCOM.BoAppEventTypes.aet_FontChanged:
					break;
				case SAPbouiCOM.BoAppEventTypes.aet_LanguageChanged:
					break;
				case SAPbouiCOM.BoAppEventTypes.aet_ServerTerminition:
					break;
				default:
					break;
			}
		}
	}
}


/*if (pVal.FormTypeEx == "UDO_FT_AutoReconcilation" && pVal.BeforeAction == false
				&& pVal.EventType == SAPbouiCOM.BoEventTypes.et_FORM_VISIBLE)
{

	*//*SAPbouiCOM.Form oForm = Application.SBO_Application.Forms.Item(FormUID);

	if (oForm != null)
	{
		foreach (SAPbouiCOM.Item item in oForm.Items)
		{
			Console.WriteLine($"Item UID: {item.UniqueID}, Item Type: {item.Type}, Item name: {item.Description}");
		}
	}*//*

	SAPbouiCOM.Form oForm = Application.SBO_Application.Forms.Item(FormUID);

	string docEntryItemUID = "0_U_S";

	SAPbouiCOM.Item docEntry = oForm.Items.Item(docEntryItemUID);

	docEntry.Enabled = false;
}*/