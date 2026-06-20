# Stock & Flow - Manual Testing Checklist

## Setup & Installation
- [ ] Clean install on fresh Windows machine
- [ ] Database creates successfully
- [ ] Settings file loads correctly
- [ ] Application starts without errors

## Core Inventory Management
- [ ] **Add Item**: Create new inventory item with all fields
- [ ] **Edit Item**: Modify existing item, verify changes persist
- [ ] **Delete Item**: Try to delete item (should fail if has sales)
- [ ] **Search**: Search by name, SKU, category
- [ ] **Low Stock**: Set item below threshold, verify it shows in low stock
- [ ] **Image Upload**: Add image to item, verify it displays

## Sales Operations
- [ ] **Record Sale**: Sell 1 item, verify:
  - [ ] Inventory decreases
  - [ ] Sale appears in sales list
  - [ ] Metrics update (revenue, profit)
- [ ] **Multiple Items Sale**: Sell 3 different items in one transaction
- [ ] **Negative Inventory**: Try to oversell (should fail if setting disabled)
- [ ] **Custom Price**: Record sale with custom price different from list price
- [ ] **Delete Sale**: Delete a sale, verify inventory restores
- [ ] **Date Range Filter**: Filter sales by date range

## Expenses
- [ ] **Add Expense**: Create expense with all fields
- [ ] **Link to Item**: Create expense linked to inventory item
- [ ] **Edit Expense**: Modify existing expense
- [ ] **Delete Expense**: Remove expense
- [ ] **Category Totals**: Verify totals calculate correctly
- [ ] **Date Range Filter**: Filter by date

## Inventory Adjustments
- [ ] **Damaged**: Record damaged goods adjustment (negative)
- [ ] **Found**: Record found inventory adjustment (positive)
- [ ] **Correction**: Record inventory correction
- [ ] **History**: View adjustment history for specific item

## Reports & Analytics
- [ ] **Dashboard Metrics**: Verify all metrics calculate:
  - [ ] Total revenue
  - [ ] Gross profit
  - [ ] Net profit
  - [ ] Inventory value
- [ ] **Date Range Reports**: Change period, verify metrics recalculate
- [ ] **Charts Display**: All charts render without errors
- [ ] **PDF Export**: Generate PDF report, verify it opens

## Import/Export
- [ ] **Export Excel**: Export inventory to Excel
  - [ ] Open file, verify data is correct
  - [ ] All columns present
- [ ] **Import Excel**: Import from Excel file
  - [ ] New items added
  - [ ] Existing items updated
  - [ ] No duplicates created
- [ ] **Import Validation**: Try importing bad data, verify error handling

## Shopify Integration (if configured)
- [ ] **Configure**: Enter Shopify credentials, save settings
- [ ] **Sync Products**: Pull products from Shopify
  - [ ] New products created
  - [ ] Existing products updated
  - [ ] Verify variant handling
- [ ] **Sync Orders**: Pull orders from Shopify
  - [ ] Orders create sales records
  - [ ] Inventory decreases
  - [ ] Customer info populated
  - [ ] No duplicate orders

## Data Integrity Tests
- [ ] **Transaction Rollback**:
  - [ ] Disconnect during save (if possible)
  - [ ] Force close app during operation
  - [ ] Verify database not corrupted
- [ ] **Concurrent Access**: Open app twice, verify file locking
- [ ] **Large Dataset**: Import 1,000 items, verify performance
- [ ] **Date Ranges**: Test with data spanning multiple years

## Settings & Configuration
- [ ] **Storage Mode**: Switch between JSON and SQLite
- [ ] **Allow Negative Inventory**: Toggle setting, verify behavior changes
- [ ] **Business Info**: Update business details
- [ ] **Shopify Settings**: Update and save Shopify credentials

## Performance Tests
- [ ] **1,000 Items**: Import 1,000 inventory items
  - [ ] Search is fast (<1 second)
  - [ ] Loading is acceptable (<3 seconds)
- [ ] **10,000 Sales**: Import 10,000 sales records
  - [ ] Reports load in <5 seconds
  - [ ] Date filtering is fast
  - [ ] Metrics calculate correctly
- [ ] **Bulk Operations**:
  - [ ] Import 100 items at once
  - [ ] Record 100 sales
  - [ ] Verify debouncing (metrics only recalc once)

## Edge Cases
- [ ] **Empty Data**: Start with no data, verify no errors
- [ ] **Special Characters**: Use names with: ' " < > & in names
- [ ] **Very Large Numbers**: Enter $999,999,999.99 prices
- [ ] **Zero Values**: Enter $0 prices, 0 quantities
- [ ] **Negative Values**: Try entering negative prices (should fail)
- [ ] **Future Dates**: Enter sales with future dates
- [ ] **Past Dates**: Enter sales from 10 years ago

## Error Handling
- [ ] **Missing Files**: Delete database, verify graceful handling
- [ ] **Corrupted Data**: Edit JSON file with bad data
- [ ] **Network Errors**: Shopify sync with no internet
- [ ] **Invalid Shopify Creds**: Try sync with wrong credentials
- [ ] **Disk Full**: Fill disk, try to save (if testable)

## Security Tests
- [ ] **SQL Injection**: Try entering SQL in text fields
- [ ] **Path Traversal**: Try entering "../../../" in file paths
- [ ] **XSS**: Try entering `<script>alert('xss')</script>` in fields
- [ ] **Encrypted Data**: Verify Shopify credentials are encrypted in settings.json

## Usability Issues
- [ ] Are error messages clear and helpful?
- [ ] Can you figure out how to use features without instructions?
- [ ] Does the UI feel responsive?
- [ ] Are there any UI glitches or visual bugs?
- [ ] Do tooltips explain what fields mean?

## Notes
Add any issues found during testing:

---

## Critical Issues (Block Release)
1.

---

## Important Issues (Should Fix)
1.

---

## Nice to Have (Can Wait)
1.

---

**Tested By**: _______________
**Date**: _______________
**Version**: _______________
