PRAGMA foreign_keys = ON;
INSERT OR IGNORE INTO CostCode(Code,Description,CreatedBy,CreatedDateUtc) VALUES
('1000','Site Work','seed',CURRENT_TIMESTAMP),('2000','Foundation','seed',CURRENT_TIMESTAMP),('3000','Framing','seed',CURRENT_TIMESTAMP),('4000','Roofing','seed',CURRENT_TIMESTAMP),('5000','Plumbing','seed',CURRENT_TIMESTAMP),('6000','Electrical','seed',CURRENT_TIMESTAMP),('7000','HVAC','seed',CURRENT_TIMESTAMP),('8000','Flooring','seed',CURRENT_TIMESTAMP),('9000','Painting','seed',CURRENT_TIMESTAMP);

INSERT INTO Vendor(VendorName,Email,Phone,CreatedBy,CreatedDateUtc)
SELECT 'Summit Concrete Works','estimating@summit-concrete.example','555-0101','seed',CURRENT_TIMESTAMP
WHERE NOT EXISTS(SELECT 1 FROM Vendor WHERE VendorName='Summit Concrete Works');
INSERT INTO Vendor(VendorName,Email,Phone,CreatedBy,CreatedDateUtc)
SELECT 'Redwood Framing Co.','orders@redwood-framing.example','555-0102','seed',CURRENT_TIMESTAMP
WHERE NOT EXISTS(SELECT 1 FROM Vendor WHERE VendorName='Redwood Framing Co.');
INSERT INTO Vendor(VendorName,Email,Phone,CreatedBy,CreatedDateUtc)
SELECT 'Pacific Crest Roofing','service@pacific-crest.example','555-0103','seed',CURRENT_TIMESTAMP
WHERE NOT EXISTS(SELECT 1 FROM Vendor WHERE VendorName='Pacific Crest Roofing');
INSERT INTO Vendor(VendorName,Email,Phone,CreatedBy,CreatedDateUtc)
SELECT 'ClearFlow Plumbing','dispatch@clearflow.example','555-0104','seed',CURRENT_TIMESTAMP
WHERE NOT EXISTS(SELECT 1 FROM Vendor WHERE VendorName='ClearFlow Plumbing');
