PRAGMA foreign_keys = ON;
BEGIN TRANSACTION;

CREATE TABLE IF NOT EXISTS CostCode (
  CostCodeId INTEGER PRIMARY KEY AUTOINCREMENT,
  Code TEXT NOT NULL UNIQUE,
  Description TEXT NOT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0,1)),
  CreatedBy TEXT NOT NULL,
  CreatedDateUtc TEXT NOT NULL,
  UpdatedBy TEXT NULL,
  UpdatedDateUtc TEXT NULL
);
CREATE TABLE IF NOT EXISTS Vendor (
  VendorId INTEGER PRIMARY KEY AUTOINCREMENT,
  VendorName TEXT NOT NULL,
  Email TEXT NULL,
  Phone TEXT NULL,
  IsActive INTEGER NOT NULL DEFAULT 1 CHECK (IsActive IN (0,1)),
  CreatedBy TEXT NOT NULL,
  CreatedDateUtc TEXT NOT NULL,
  UpdatedBy TEXT NULL,
  UpdatedDateUtc TEXT NULL
);
CREATE TABLE IF NOT EXISTS Estimate (
  EstimateId INTEGER PRIMARY KEY AUTOINCREMENT,
  EstimateNumber TEXT NOT NULL UNIQUE,
  ProjectName TEXT NOT NULL,
  Description TEXT NULL,
  Status TEXT NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Approved')),
  Version INTEGER NOT NULL DEFAULT 1,
  CreatedBy TEXT NOT NULL,
  CreatedDateUtc TEXT NOT NULL,
  UpdatedBy TEXT NULL,
  UpdatedDateUtc TEXT NULL
);
CREATE TABLE IF NOT EXISTS EstimateLine (
  EstimateLineId INTEGER PRIMARY KEY AUTOINCREMENT,
  EstimateId INTEGER NOT NULL REFERENCES Estimate(EstimateId),
  CostCodeId INTEGER NOT NULL REFERENCES CostCode(CostCodeId),
  Description TEXT NULL,
  Quantity NUMERIC NOT NULL DEFAULT 1 CHECK (Quantity > 0),
  UnitCost NUMERIC NOT NULL CHECK (UnitCost >= 0)
);
CREATE INDEX IF NOT EXISTS IX_EstimateLine_EstimateId ON EstimateLine(EstimateId);
CREATE INDEX IF NOT EXISTS IX_EstimateLine_CostCodeId ON EstimateLine(CostCodeId);
CREATE TABLE IF NOT EXISTS Job (
  JobId INTEGER PRIMARY KEY AUTOINCREMENT,
  JobNumber TEXT NOT NULL UNIQUE,
  EstimateId INTEGER NOT NULL UNIQUE REFERENCES Estimate(EstimateId),
  JobName TEXT NOT NULL,
  Status TEXT NOT NULL DEFAULT 'Active' CHECK (Status IN ('Active','Closed')),
  Version INTEGER NOT NULL DEFAULT 1,
  CreatedBy TEXT NOT NULL,
  CreatedDateUtc TEXT NOT NULL,
  UpdatedBy TEXT NULL,
  UpdatedDateUtc TEXT NULL
);
CREATE TABLE IF NOT EXISTS JobBudgetLine (
  JobBudgetLineId INTEGER PRIMARY KEY AUTOINCREMENT,
  JobId INTEGER NOT NULL REFERENCES Job(JobId),
  CostCodeId INTEGER NOT NULL REFERENCES CostCode(CostCodeId),
  Description TEXT NULL,
  BudgetAmount NUMERIC NOT NULL CHECK (BudgetAmount >= 0)
);
CREATE INDEX IF NOT EXISTS IX_JobBudgetLine_JobId_CostCodeId ON JobBudgetLine(JobId,CostCodeId);
CREATE TABLE IF NOT EXISTS PurchaseOrder (
  PurchaseOrderId INTEGER PRIMARY KEY AUTOINCREMENT,
  PONumber TEXT NOT NULL UNIQUE,
  JobId INTEGER NOT NULL REFERENCES Job(JobId),
  VendorId INTEGER NOT NULL REFERENCES Vendor(VendorId),
  Status TEXT NOT NULL DEFAULT 'Draft' CHECK (Status IN ('Draft','Issued','Closed')),
  Version INTEGER NOT NULL DEFAULT 1,
  CreatedBy TEXT NOT NULL,
  CreatedDateUtc TEXT NOT NULL,
  UpdatedBy TEXT NULL,
  UpdatedDateUtc TEXT NULL
);
CREATE INDEX IF NOT EXISTS IX_PO_JobId_Status ON PurchaseOrder(JobId,Status);
CREATE TABLE IF NOT EXISTS PurchaseOrderLine (
  PurchaseOrderLineId INTEGER PRIMARY KEY AUTOINCREMENT,
  PurchaseOrderId INTEGER NOT NULL REFERENCES PurchaseOrder(PurchaseOrderId),
  CostCodeId INTEGER NOT NULL REFERENCES CostCode(CostCodeId),
  Description TEXT NULL,
  Amount NUMERIC NOT NULL CHECK (Amount > 0)
);
CREATE INDEX IF NOT EXISTS IX_POLine_POId ON PurchaseOrderLine(PurchaseOrderId);
CREATE INDEX IF NOT EXISTS IX_POLine_CostCodeId ON PurchaseOrderLine(CostCodeId);
CREATE TABLE IF NOT EXISTS JobCostTransaction (
  JobCostTransactionId INTEGER PRIMARY KEY AUTOINCREMENT,
  JobId INTEGER NOT NULL REFERENCES Job(JobId),
  CostCodeId INTEGER NOT NULL REFERENCES CostCode(CostCodeId),
  VendorId INTEGER NULL REFERENCES Vendor(VendorId),
  PurchaseOrderId INTEGER NULL REFERENCES PurchaseOrder(PurchaseOrderId),
  ReferenceNumber TEXT NULL,
  Description TEXT NULL,
  TransactionDate TEXT NOT NULL,
  Amount NUMERIC NOT NULL CHECK (Amount > 0),
  CreatedBy TEXT NOT NULL,
  CreatedDateUtc TEXT NOT NULL
);
CREATE INDEX IF NOT EXISTS IX_JobCost_JobId_CostCodeId ON JobCostTransaction(JobId,CostCodeId);
CREATE INDEX IF NOT EXISTS IX_JobCost_POId ON JobCostTransaction(PurchaseOrderId);
COMMIT;
