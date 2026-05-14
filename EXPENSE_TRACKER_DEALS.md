# German Supermarket Deal Receipt Scanner

## Project Overview

A web-based expense tracker with receipt scanning that integrates German supermarket deals (Aldi, Lidl, Rewe, Edeka) to provide personalized recommendations based on your regular purchases.

**Core Value Proposition:**
- Scan receipts to track spending automatically
- Extract purchased items and prices
- Match items against current supermarket deals in your area
- Get recommendations for deals on products you buy regularly

---

## MVP Development Phases

### Phase 1: Receipt Scanning & Expense Tracking (Foundation)
**Goal:** Build the core receipt capture and item tracking system

**Features:**
- Receipt image upload/camera capture on mobile
- OCR text extraction from receipt images
- Parse receipt data (items, prices, store, date)
- Store purchase history in local database
- Basic expense dashboard showing spending over time
- Item frequency analysis (what you buy regularly)

**Technical Stack:**
- C# ASP.NET Backend with Blazor frontend
- Tesseract OCR or similar for receipt text extraction
- PostgreSQL database for local storage
- ??? for mobile camera access

**Database Schema:**
```
Receipts:
- receipt_id, store, date, total_amount, image_path

Items:
- item_id, receipt_id, product_name, category, quantity, price, unit

Purchase History:
- item_name, purchase_count, avg_price, last_purchased_date
```

**Challenges:**
- German receipt formats vary by store
- OCR accuracy with poor lighting or angled photos
- Parsing product names to standardize them
- Handling bulk items and discounts correctly

---

### Phase 2: Manual Deal Database & Matching (User-Driven)
**Goal:** Build deal-to-purchase matching without external APIs

**Features:**
- Weekly flyer upload system (users upload PDF/images)
- Deal catalog with tags (product, retailer, location, validity dates)
- Smart matching between purchases and available deals
- Deal recommendations based on your purchase history
- Location filtering (show deals near you)
- Savings calculator (potential savings based on your buying habits)

**Database Additions:**
```
Deals:
- deal_id, product_name, retailer, regular_price, deal_price, 
  valid_from, valid_to, location_zip, image_url, source

DealMatches:
- match_id, purchase_item_id, deal_id, savings_potential
```

**Features:**
- Community voting on deal usefulness
- Regional deal communities
- Push notifications for deals matching your purchases

**Challenges:**
- Standardizing product names across retailers
- Handling regional variations (Aldi Süd vs. Nord)
- Maintaining data accuracy and freshness

---

### Phase 3: Automated Deal Source Integration (Scalable)
**Goal:** Automate deal collection from official sources

**Integration Options (Ranked by Feasibility):**

**1. Third-Party Deal Aggregator APIs**
- **Kaufda.de** - Investigate API availability for partners
- **Marktguru** - German deal platform with potential API
- **MeinProspekt.de** - Aggregates weekly flyers
- Benefit: Legal, reliable, official data
- Challenge: May require partnership or paid access

**2. Official Retailer Data**
- **Aldi**: Regional flyer systems (Aldi Süd, Aldi Nord)
  - May have limited API or data feeds
  - Website flyers can be accessed programmatically
- **Lidl**: Weekly flyer system on website
- **Rewe**: More open platform with online presence
- **Edeka**: Regional approach, varying data availability
- Benefit: Official source, most accurate
- Challenge: Not all have public APIs; ToS may restrict scraping

**3. Web Scraping (Last Resort)**
- Scrape weekly flyer data from retailer websites
- Respectful scraping (follow robots.txt, rate limiting)
- Extract deal info from HTML/PDFs
- Benefit: Data-agnostic, works regardless of API availability
- Challenge: Legal gray area, requires maintenance

**Recommended Priority:**
1. Contact Kaufda/Marktguru for API partnerships
2. Develop scrapers for publicly available flyer data
3. Build fallback to community data from Phase 2

---

### Data Flow
```
1. User uploads receipt image
   ↓
2. OCR extracts items & prices
   ↓
3. Items stored in database
   ↓
4. Background job matches against deals
   ↓
5. Recommendations generated
   ↓
6. User sees "You bought X regularly, it's on sale at Y for Z savings"
```

---

## Key Features & User Stories

### Receipt Scanning
- **As a user:** I want to photograph a receipt and have items extracted automatically
- **Acceptance:** Items, prices, and store are correctly parsed from receipt image

### Purchase History
- **As a user:** I want to see what I buy regularly and spending patterns
- **Acceptance:** Frequency analysis shows top 20 items, spending trends over time

### Deal Matching
- **As a user:** I want to see deals on products I buy regularly
- **Acceptance:** When scanning receipts, system alerts me to relevant current deals

### Location-Based Deals
- **As a user:** I want to see deals available in my area
- **Acceptance:** Deals filtered by postal code/radius, showing nearby supermarkets

### Savings Dashboard
- **As a user:** I want to know how much I could save by shopping deals
- **Acceptance:** "You could save €X/month by buying these items on sale"

---

## Technical Considerations

### Challenges
1. **OCR Accuracy**
   - Solution: User can manually correct parsed items
   - Use Google Vision API if OCR quality issues
   - Train model on German receipts

2. **Product Name Standardization**
   - Solution: Build product taxonomy/database
   - Fuzzy matching for similar product names
   - User feedback loop to improve matching

3. **Regional Deal Variations**
   - Aldi Süd ≠ Aldi Nord (different deals)
   - Edeka is regional/franchise-based
   - Solution: Store location data, allow users to set their region

4. **Data Freshness**
   - Deals change weekly across all retailers
   - Solution: Automated weekly deal updates, user can manually refresh
   - Cache strategically to avoid API overload

5. **Privacy Concerns**
   - Users may not want to share receipt data
   - Solution: Keep all data local on device, encrypt if cloud-synced
   - Clear privacy policy, no third-party data sharing

---

## Future Enhancements

- Mobile app (React Native) alongside web app
- Multi-user household support with shared wishlist
- AI-powered budget alerts ("You're spending 20% more on dairy this month")
- Integration with smart shopping lists
- Loyalty program tracking (Payback, DM, etc.)
- Price history charts
- Environmental impact tracking
- Export/integration with budgeting apps (Finanzguru, etc.)
- Conform to german data handling rules/laws (are there certain certifications?)

---

## References & Resources

**German Supermarket APIs to Investigate:**
- Kaufda.de partnership inquiries
- Marktguru API documentation
- MeinProspekt data availability
- Individual retailer developer programs

**German Deal Communities:**
- mydealz.de (user-driven deal platform - reference for community features)
- Idealo.de (price comparison)