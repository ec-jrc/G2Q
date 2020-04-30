# Advanced use of the connector

## Modifying the script

### Column names
In GDX files it can happen that several columns have the same name, which is not permitted in Qlik. To solve this problem, the connector adds a numeric value to the names that are duplicated.

For example, if there are three columns with the name COUNTRY, they will appear as @COUNTRY0, @COUNTRY1 and @COUNTRY2
Column's with the name * will always be transformed in this way, being @*0, @*1

### Reference by position
Columns can also be referenced by position (starting with 0) instead of name. For example this code would load the first two columns of the symbol:

```
SQL SELECT "@0", 
    "@1"
FROM "symbol_name <path_to_file>";
```

### Filter results
Filtering is allowed using a WHERE clause with the following syntax
```
SQL SELECT "field_1", 
    "field_2",
    "Value"
FROM "symbol_name <path_to_file>"
WHERE "field_1" = 'value' 
AND "field_2" = '';
```
It has some limitations:

* Only dimensions / domains can be filtered
* The value has to appear between single quotes
* The only operation available is equals
* The empty value represented with the empty string, i.e. ''
