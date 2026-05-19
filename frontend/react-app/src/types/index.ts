export interface Receipt {
  id: string;
  userId: string;
  storeName: string;
  createdAt: string;
  status: 'Pending' | 'Processing' | 'Processed';
  totalAmount: number;
  items: ReceiptItem[];
}

export interface ReceiptItem {
  id: string;
  description: string;
  quantity: number;
  unitPrice: number;
  totalPrice: number;
}

export interface ReceiptSummary {
  id: string;
  storeName: string;
  createdAt: string;
  status: 'Pending' | 'Processing' | 'Processed';
  totalAmount: number;
}

export interface DealMatch {
  id: string;
  receiptId: string;
  dealId: string;
  dealTitle: string;
  discountAmount: number;
  matchedItemDescription: string;
}

export interface Notification {
  id: string;
  userId: string;
  receiptId: string;
  message: string;
  isRead: boolean;
  createdAt: string;
}
