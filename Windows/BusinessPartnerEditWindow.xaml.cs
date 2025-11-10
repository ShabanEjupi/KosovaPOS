using System;
using System.Windows;
using KosovaPOS.Database;
using KosovaPOS.Models;

namespace KosovaPOS.Windows
{
    public partial class BusinessPartnerEditWindow : Window
    {
        private BusinessPartner? _partner;
        
        public BusinessPartnerEditWindow(BusinessPartner? partner = null)
        {
            InitializeComponent();
            _partner = partner;
            
            PartnerTypeComboBox.Items.Add("Klient");
            PartnerTypeComboBox.Items.Add("Furnizues");
            PartnerTypeComboBox.Items.Add("Të dy");
            PartnerTypeComboBox.SelectedIndex = 0;
            
            if (_partner != null)
            {
                Title = "Ndrysho partnerin";
                LoadPartnerData();
            }
        }
        
        private void LoadPartnerData()
        {
            if (_partner == null) return;
            
            NameTextBox.Text = _partner.Name;
            NRFTextBox.Text = _partner.NRF;
            NUITextBox.Text = _partner.NUI;
            AddressTextBox.Text = _partner.Address;
            CityTextBox.Text = _partner.City;
            PhoneTextBox.Text = _partner.Phone;
            EmailTextBox.Text = _partner.Email;
            PartnerTypeComboBox.Text = _partner.PartnerType;
            IsActiveCheckBox.IsChecked = _partner.IsActive;
        }
        
        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(NameTextBox.Text))
            {
                MessageBox.Show("Ju lutem shkruani emrin e partnerit!", "Vërejtje",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                using var context = new POSDbContext();
                
                BusinessPartner partner;
                if (_partner != null)
                {
                    partner = context.BusinessPartners.Find(_partner.Id);
                    if (partner == null) return;
                }
                else
                {
                    partner = new BusinessPartner();
                    context.BusinessPartners.Add(partner);
                }
                
                partner.Name = NameTextBox.Text;
                partner.NRF = NRFTextBox.Text;
                partner.NUI = NUITextBox.Text;
                partner.Address = AddressTextBox.Text;
                partner.City = CityTextBox.Text;
                partner.Phone = PhoneTextBox.Text;
                partner.Email = EmailTextBox.Text;
                partner.PartnerType = PartnerTypeComboBox.Text;
                partner.IsActive = IsActiveCheckBox.IsChecked ?? true;
                
                context.SaveChanges();
                
                MessageBox.Show("Partneri u ruajt me sukses!", "Sukses",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                
                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gabim gjatë ruajtjes: {ex.Message}", "Gabim",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
