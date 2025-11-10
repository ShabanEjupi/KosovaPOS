using System.Windows;

namespace KosovaPOS.Windows
{
    public partial class DiscountWindow : Window
    {
        public decimal DiscountPercent { get; private set; }
        private readonly decimal _originalPrice;

        public DiscountWindow(string articleName, decimal originalPrice, decimal currentDiscount = 0)
        {
            InitializeComponent();
            
            _originalPrice = originalPrice;
            ArticleNameText.Text = articleName;
            OriginalPriceText.Text = $"{originalPrice:F2} €";
            
            // Set current discount if any
            if (currentDiscount > 0)
            {
                DiscountPercentTextBox.Text = currentDiscount.ToString("F2");
            }
            
            UpdateFinalPrice();
            
            // Focus on discount textbox
            DiscountPercentTextBox.Focus();
            DiscountPercentTextBox.SelectAll();
        }

        private void DiscountPercent_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateFinalPrice();
        }

        private void UpdateFinalPrice()
        {
            if (decimal.TryParse(DiscountPercentTextBox.Text, out decimal discount))
            {
                // Validate discount range
                if (discount < 0)
                {
                    discount = 0;
                    DiscountPercentTextBox.Text = "0";
                }
                else if (discount > 100)
                {
                    discount = 100;
                    DiscountPercentTextBox.Text = "100";
                }

                var discountValue = _originalPrice * (discount / 100);
                var finalPrice = _originalPrice - discountValue;
                
                FinalPriceText.Text = $"{finalPrice:F2} €";
            }
            else
            {
                FinalPriceText.Text = $"{_originalPrice:F2} €";
            }
        }

        private void Apply_Click(object sender, RoutedEventArgs e)
        {
            if (decimal.TryParse(DiscountPercentTextBox.Text, out decimal discount))
            {
                // Validate range
                if (discount < 0 || discount > 100)
                {
                    MessageBox.Show("Zbritja duhet të jetë mes 0 dhe 100%.", 
                        "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DiscountPercent = discount;
                DialogResult = true;
                Close();
            }
            else
            {
                MessageBox.Show("Ju lutem vendosni një vlerë numerike për zbritjen.", 
                    "Gabim", MessageBoxButton.OK, MessageBoxImage.Warning);
                DiscountPercentTextBox.Focus();
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
